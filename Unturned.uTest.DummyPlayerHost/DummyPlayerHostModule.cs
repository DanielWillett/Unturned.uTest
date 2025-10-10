using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using HarmonyLib;
using SDG.Framework.Modules;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Module;
using uTest.Protocol.DummyPlayerHost;

namespace uTest.Dummies.Host;

internal class DummyPlayerHost : IServiceProvider, IDisposable
{
    public Harmony Harmony { get; } = new Harmony("DanielWillett.uTest.Dummies");

    public ILogger Logger => DefaultLogger.Logger;

#nullable disable

    private NamedPipeClientsideRemoteRpcConnection _rpcConnection;
    private IRpcRouter _rpcRouter;
    private IRpcSerializer _rpcSerializer;
    private IRpcConnectionLifetime _rpcConnectionLifetime;
    private NamedPipeEndpoint _rpcEndpoint;

    public static DummyPlayerHost Instance { get; private set; }

    public AssetLoadModel AssetLoadModel { get; private set; }

#nullable restore

    object? IServiceProvider.GetService(Type serviceType)
    {
        return null;
    }

    internal void Initialize()
    {
        Instance = this;

        ProxyGenerator.Instance.SetLogger(Log);

        AssetLoadModel = new AssetLoadModel();

        Patches.SkipAddFoundAssetIfNotRequired.TryPatch(Harmony, ConsoleLogger.Instance);

        _rpcConnectionLifetime = new ClientRpcConnectionLifetime();
        _rpcSerializer = new DefaultSerializer();

        DependencyInjectionRpcRouter rpcRouter = new DependencyInjectionRpcRouter(this, _rpcSerializer, _rpcConnectionLifetime, ProxyGenerator.Instance);
        rpcRouter.SetLogger(Log);

        _rpcEndpoint = NamedPipeEndpoint.AsClient(NamedPipe.PipeName);

        CommandWindow.Log("Connecting to server running...");

        Task<NamedPipeClientsideRemoteRpcConnection> connectTask
            = _rpcEndpoint.RequestConnectionAsync(_rpcRouter, _rpcConnectionLifetime, _rpcSerializer, TimeSpan.FromMinutes(1d));
        if (!connectTask.Wait(TimeSpan.FromMinutes(1.01d)))
            throw new TimeoutException("Timed out connecting.");
        _rpcConnection = connectTask.Result;

        CommandWindow.Log("Connected.");

        _rpcRouter = rpcRouter;
    }

    private static void Log(Type sourceType, DanielWillett.ModularRpcs.LogSeverity severity, Exception? exception, string? message)
    {
        if (exception != null)
        {
            switch (severity)
            {
                default:
                    CommandWindow.Log($"[INF] [{sourceType.Name}] {message}{System.Environment.NewLine}{exception}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Debug:
                    CommandWindow.Log($"[DBG] [{sourceType.Name}] {message}{System.Environment.NewLine}{exception}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Warning:
                    CommandWindow.LogWarning($"[WRN] [{sourceType.Name}] {message}{System.Environment.NewLine}{exception}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Error:
                    CommandWindow.LogError($"[ERR] [{sourceType.Name}] {message}{System.Environment.NewLine}{exception}");
                    break;
            }
        }
        else
        {
            switch (severity)
            {
                default:
                    CommandWindow.Log($"[INF] [{sourceType.Name}] {message}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Debug:
                    CommandWindow.Log($"[DBG] [{sourceType.Name}] {message}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Warning:
                    CommandWindow.LogWarning($"[WRN] [{sourceType.Name}] {message}");
                    break;

                case DanielWillett.ModularRpcs.LogSeverity.Error:
                    CommandWindow.LogError($"[ERR] [{sourceType.Name}] {message}");
                    break;
            }
        }
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        if (Instance == this)
            Instance = null;
        Harmony.UnpatchAll(Harmony.Id);
    }
}

internal sealed class DummyPlayerHostModule : IModuleNexus
{
    // allow catching TypeLoadException errors
    private object? _module;

    void IModuleNexus.initialize()
    {
        try
        {
            Init();
        }
        catch (Exception ex)
        {
            CommandWindow.Log("Startup error.");
            CommandWindow.Log(ex);

            InstantShutdown(ex.GetType().Name, UnturnedTestExitCode.StartupFailure, () =>
            {
                (_module as IDisposable)?.Dispose();
            });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Init()
    {
        DummyPlayerHost host = new DummyPlayerHost();
        host.Initialize();
        _module = host;
    }

    void IModuleNexus.shutdown()
    {
        (_module as IDisposable)?.Dispose();
    }

    internal static void InstantShutdown(string reason, UnturnedTestExitCode exitCode, Action shutdown)
    {
        GameThread.Assert();

        FieldInfo? wasQuitGameCalled = typeof(Provider).GetField("wasQuitGameCalled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (wasQuitGameCalled != null)
        {
            wasQuitGameCalled.SetValue(null, true);
        }
        else
        {
            CommandWindow.LogWarning("uTest failed to find field 'Provider.wasQuitGameCalled'.");
        }

        UnturnedLog.info($"uTest Quit game: {reason}. Exit code: {(int)exitCode} ({exitCode}).");
        try
        {
            shutdown();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("Shutdown error");
            CommandWindow.LogError(ex);
        }
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}