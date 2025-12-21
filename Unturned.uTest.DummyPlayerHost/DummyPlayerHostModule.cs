using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using HarmonyLib;
using SDG.Framework.Modules;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Dummies.Host.Facades;
using uTest.Module;
using uTest.Protocol.DummyPlayerHost;

namespace uTest.Dummies.Host;

/// <summary>
/// DummyPlayerHost is used to host one or more real players on an instance of the Unturned client.
/// </summary>
[GenerateRpcSource]
internal partial class DummyPlayerHost : IDisposable
{
    public Harmony Harmony { get; } = new Harmony("DanielWillett.uTest.Dummies");

    public ILogger Logger => DefaultLogger.Logger;

#nullable disable

    private NamedPipeClientsideRemoteRpcConnection _rpcConnection;
    private IRpcRouter _rpcRouter;
    private IRpcSerializer _rpcSerializer;
    private IRpcConnectionLifetime _rpcConnectionLifetime;
    private NamedPipeEndpoint _rpcEndpoint;

    private readonly CommandLineString _steamIdArg = new CommandLineString("-uTestSteamId");
    private readonly CommandLineString _configArg = new CommandLineString("-uTestConfig");

    public static DummyPlayerHost Instance { get; private set; }

    public AssetLoadModel AssetLoadModel { get; private set; }

    public CSteamID SteamId { get; private set; }

    public string TemporaryDataPath { get; private set; }

    public ulong[] WorkshopItems { get; private set; }

#nullable restore

    internal void Initialize()
    {
        if (!ulong.TryParse(_steamIdArg.value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId))
        {
            throw new InvalidOperationException($"Missing \"{_steamIdArg.key}\" command line arg.");
        }

        SteamId = new CSteamID(steamId);
        if (SteamId.GetEAccountType() != EAccountType.k_EAccountTypeIndividual)
        {
            throw new InvalidOperationException($"Invalid SteamID {steamId} in \"{_steamIdArg.key}\" command line arg.");
        }

        Instance = this;

        ProxyGenerator.Instance.SetLogger(UnturnedLogLogger.Instance);

        AssetLoadModel = new AssetLoadModel();

        Patches.SkipAddFoundAssetIfNotRequired.TryPatch(Harmony, ConsoleLogger.Instance);

        ServiceContainer cont = new ServiceContainer();

        ClientRpcConnectionLifetime lifetime = new ClientRpcConnectionLifetime();
        _rpcConnectionLifetime = lifetime;
        _rpcSerializer = new DefaultSerializer();

        lifetime.SetLogger(UnturnedLogLogger.Instance);

        cont.AddService(typeof(ProxyGenerator), ProxyGenerator.Instance);
        cont.AddService(typeof(IRpcConnectionLifetime), lifetime);
        cont.AddService(typeof(IRpcSerializer), _rpcSerializer);

        DependencyInjectionRpcRouter rpcRouter = new DependencyInjectionRpcRouter(cont);
        cont.AddService(typeof(IRpcRouter), rpcRouter);
        rpcRouter.SetLogger(UnturnedLogLogger.Instance);

        _rpcEndpoint = NamedPipeEndpoint.AsClient(NamedPipe.PipeName);

        CommandWindow.Log("Connecting to server running...");

        Task<NamedPipeClientsideRemoteRpcConnection> connectTask
            = _rpcEndpoint.RequestConnectionAsync(_rpcRouter, _rpcConnectionLifetime, _rpcSerializer, TimeSpan.FromMinutes(1d));
        if (!connectTask.Wait(TimeSpan.FromSeconds(61d)))
            throw new TimeoutException("Timed out connecting.");
        _rpcConnection = connectTask.Result;

        CommandWindow.Log("Connected.");

        _rpcRouter = rpcRouter;

        LocalWorkshopSettings.instance = new DummyLocalWorkshopSettings(this);

        DummyProvider.ShutdownOldProviderServices(Provider.provider);
        Provider.provider = new DummyProvider(this);


        try
        {
            Task.Run(async () =>
            {
                await SendStatusNotification(SteamId.m_SteamID, DummyReadyStatus.StartedUp);
            }).Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            UnturnedLogLogger.Instance.LogError("Error communicating with server.", ex);
            DummyPlayerHostModule.InstantShutdown(
                "Error communicating with server.",
                UnturnedTestExitCode.StartupFailure,
                () => ((IDisposable)this).Dispose()
            );
        }
    }

    [RpcSend(typeof(DummyPlayerLauncher), "ReceiveStatusNotification")]
    [RpcTimeout(2 * Timeouts.Seconds)]
    private partial RpcTask SendStatusNotification(ulong id, DummyReadyStatus status);

    [RpcReceive]
    private void ReceiveWorkshopItemsUpdate(ulong[] workshopItems)
    {
        GameThread.Run(workshopItems, workshopItems =>
        {
            WorkshopItems = workshopItems;
        });
    }

    [RpcReceive]
    private void ReceiveConnectRequest(string ipAddress, ulong connectCode, ushort port, string password)
    {

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