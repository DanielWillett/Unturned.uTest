using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using DanielWillett.ReflectionTools;
using HarmonyLib;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using SDG.Framework.Utilities;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.SceneManagement;
using Unturned.SystemEx;
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
    private NamedPipeEndpoint _rpcEndpoint;

    private readonly CommandLineString _steamIdArg = new CommandLineString("-uTestSteamId");
    private readonly CommandLineString _dataDirArg = new CommandLineString("-uTestDataDir");

    public static DummyPlayerHost Instance { get; private set; }

    public AssetLoadModel AssetLoadModel { get; private set; }

    public CSteamID SteamId { get; private set; }

    public string TemporaryDataPath { get; private set; }

    public ulong[] WorkshopItems { get; private set; }

    public DummyLauncherConfig LaunchConfig { get; private set; }

    public IModularRpcRemoteConnection Connection => _rpcConnection;

#nullable restore

    internal void Initialize(IServiceProvider serviceProvider)
    {
        TimeUtility.updated += GameThread.RunContinuations;

        if (!ulong.TryParse(_steamIdArg.value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId))
        {
            throw new InvalidOperationException($"Missing \"{_steamIdArg.key}\" command line arg.");
        }

        SteamId = new CSteamID(steamId);
        if (SteamId.GetEAccountType() != EAccountType.k_EAccountTypeIndividual)
        {
            throw new InvalidOperationException($"Invalid SteamID {steamId} in \"{_steamIdArg.key}\" command line arg.");
        }

        if (string.IsNullOrEmpty(_dataDirArg.value))
        {
            throw new InvalidOperationException($"Missing \"{_dataDirArg.key}\" command line arg.");
        }

        TemporaryDataPath = Path.GetFullPath(_dataDirArg.value);
        string configFile = Path.Combine(TemporaryDataPath, "startup.json");

        using (JsonTextReader reader = new JsonTextReader(new StreamReader(configFile, Encoding.UTF8, true, 1024)))
        {
            reader.CloseInput = true;

            JsonSerializer serializer = JsonSerializer.CreateDefault();
            LaunchConfig = serializer.Deserialize<DummyLauncherConfig>(reader);
            if (LaunchConfig == null || LaunchConfig.WorkshopIds == null || LaunchConfig.Steam64 != steamId)
            {
                throw new InvalidOperationException($"Missing or invalid launch config at \"{configFile}\".");
            }
        }

        WorkshopItems = LaunchConfig.WorkshopIds;

        Instance = this;

        ProxyGenerator.Instance.SetLogger(DefaultLoggerReflectionTools.Logger);

        AssetLoadModel = new AssetLoadModel();

        Patches.SkipAddFoundAssetIfNotRequired.TryPatch(Harmony, ConsoleLogger.Instance);

        _rpcEndpoint = NamedPipeEndpoint.AsClient(serviceProvider, NamedPipe.PipeName);

        CommandWindow.Log($"Connecting to uTest server via Named Pipes as {steamId}...");

        Task<NamedPipeClientsideRemoteRpcConnection> connectTask = _rpcEndpoint.RequestConnectionAsync(TimeSpan.FromSeconds(15d));
        if (!connectTask.Wait(TimeSpan.FromSeconds(16d)))
            throw new TimeoutException("Timed out connecting.");
        _rpcConnection = connectTask.Result;

        CommandWindow.Log("Connected.");

        LocalWorkshopSettings.instance = new DummyLocalWorkshopSettings(this);

        // todo Provider._client = SteamId;
        // todo Provider._user = SteamId;

        DummyProvider.ShutdownOldProviderServices(Provider.provider);
        Provider.provider = new DummyProvider(this);

        StartStatusNotificationUpdate(DummyReadyStatus.StartedUp);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void StartStatusNotificationUpdate(DummyReadyStatus status)
    {
        try
        {
            Task.Run(async () =>
            {
                await SendStatusNotification(SteamId.m_SteamID, status);
            }).Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            DefaultLogger.Logger.LogError("Error communicating with server.", ex);
            DummyPlayerHostModule.InstantShutdown(
                "Error communicating with server.",
                UnturnedTestExitCode.StartupFailure,
                () => ((IDisposable)this).Dispose()
            );
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // once the menu loads we can assume we've finished loading assets
        if (scene.buildIndex != Level.BUILD_INDEX_MENU)
            return;

        StartStatusNotificationUpdate(DummyReadyStatus.AssetsLoaded);
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
    private async Task ReceiveConnect(uint ipv4, ushort port, string? password,
        ulong serverCode,
        string map,
        ECameraMode cameraMode,
        bool isPvP,
        string name,
        bool hasCheats,
        bool isWorkshop,
        EGameMode mode,
        int currentPlayers,
        int maxPlayers,
        bool hasPassword,
        bool isPro,
        SteamServerAdvertisement.EAnycastProxyMode anycastMode,
        EServerMonetizationTag monetization,
        bool vacSecure,
        bool battleyeSecure,
        SteamServerAdvertisement.EPluginFramework pluginFramework,
        string thumbnailUrl,
        string descText
    )
    {
        await GameThread.Switch();

        IPv4Address addr = new IPv4Address(ipv4);
        Logger.LogInformation($"Received request connection to {addr}:{port} (ID {serverCode}).");

        Provider.connect(
            new ServerConnectParameters(addr, (ushort)(port - 1), port, password ?? string.Empty),
            new SteamServerAdvertisement("Unturned.uTest", EGameMode.NORMAL, false, false, false)
            {
                _steamID = new CSteamID(serverCode),
                _ip = ipv4,
                connectionPort = port,
                queryPort = (ushort)(port - 1),
                _name = name,
                _map = map,
                _isPvP = isPvP,
                _hasCheats = hasCheats,
                _isWorkshop = isWorkshop,
                _mode = mode,
                _cameraMode = cameraMode,
                _pingMs = 0,
                sortingPing = 0,
                _players = currentPlayers,
                _maxPlayers = maxPlayers,
                _isPassworded = hasPassword,
                _isPro = isPro,
                utilityScore = 0,
                infoSource = SteamServerAdvertisement.EInfoSource.DirectConnect,
                anycastProxyMode = anycastMode,
                monetization = monetization,
                IsVACSecure = vacSecure,
                IsBattlEyeSecure = battleyeSecure,
                networkTransport = "sys",
                pluginFramework = pluginFramework,
                thumbnailURL = thumbnailUrl,
                descText = descText
            },
            WorkshopItems.Select(x => new PublishedFileId_t(x)).ToList()
        );
    }

    [RpcReceive]
    private async Task ReceiveDisconnect()
    {
        await GameThread.Switch();

        Provider.disconnect();
    }

    [RpcReceive]
    private Task ReceiveGracefullyClose()
    {
        Logger.LogInformation("Received graceful shutdown request from uTest server.");
        return GameThread.RunAndWaitAsync(Provider.shutdown);
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        TimeUtility.updated -= GameThread.RunContinuations;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
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
            GameThread.Setup();
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
        ServiceContainer cont = new ServiceContainer();

        ClientRpcConnectionLifetime lifetime = new ClientRpcConnectionLifetime();
        DefaultSerializer rpcSerializer = new DefaultSerializer();

        Accessor.Logger = DefaultLoggerReflectionTools.Logger;

        ProxyGenerator.Instance.SetLogger(DefaultLoggerReflectionTools.Logger);
        lifetime.SetLogger(DefaultLoggerReflectionTools.Logger);

        cont.AddService(typeof(ProxyGenerator), ProxyGenerator.Instance);
        cont.AddService(typeof(IRpcConnectionLifetime), lifetime);
        cont.AddService(typeof(IRpcSerializer), rpcSerializer);

        DependencyInjectionRpcRouter rpcRouter = new DependencyInjectionRpcRouter(cont);
        cont.AddService(typeof(IRpcRouter), rpcRouter);
        rpcRouter.SetLogger(DefaultLoggerReflectionTools.Logger);

        DummyPlayerHost host = ProxyGenerator.Instance.CreateProxy<DummyPlayerHost>(rpcRouter);
        cont.AddService(typeof(DummyPlayerHost), host);
        _module = host;

        host.Initialize(cont);
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