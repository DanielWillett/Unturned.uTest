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
using DanielWillett.ReflectionTools.Formatting;
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
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine.SceneManagement;
using Unturned.SystemEx;
using uTest.Dummies.Host.Facades;
using uTest.Dummies.Host.Patches;
using uTest.Module;

namespace uTest.Dummies.Host;

/// <summary>
/// DummyPlayerHost is used to host one or more real players on an instance of the Unturned client.
/// </summary>
[GenerateRpcSource]
internal partial class DummyPlayerHost : IDisposable
{
    internal byte[]? HWIDs;

    public Harmony Harmony { get; } = new Harmony("DanielWillett.uTest.Dummies");

    public ILogger Logger => DefaultLogger.Logger;

#nullable disable

    private NamedPipeClientsideRemoteRpcConnection _rpcConnection;
    private NamedPipeEndpoint _rpcEndpoint;
    private uTest.Patches.UnturnedTestPatches _patches;

    private readonly CommandLineString _steamIdArg = new CommandLineString("-uTestSteamId");
    private readonly CommandLineString _dataDirArg = new CommandLineString("-uTestDataDir");

    public static DummyPlayerHost Instance { get; private set; }

    public CSteamID SteamId { get; private set; }

    public string TemporaryDataPath { get; private set; }

    public ulong[] WorkshopItems { get; private set; }

    public DummyLauncherConfig LaunchConfig { get; private set; }

    public IModularRpcRemoteConnection Connection => _rpcConnection;

    /// <summary>
    /// The Unity window handle ('HWND') if currently on Windows. This is unused on other platforms.
    /// </summary>
    public nint WindowHandle { get; internal set; }

    internal ReadyToConnectInfo ReadyToConnectInfo { get; private set; }

#nullable restore

    public AssetLoadModel? AssetLoadModel { get; private set; }

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

        Assembly.Load("ModularRPCs.Unity, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

        Instance = this;

        TemporaryDataPath = Path.GetFullPath(_dataDirArg.value);
        Logs.setLogFilePath(Path.Combine(TemporaryDataPath, "Client.log"));
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

        ProxyGenerator.Instance.SetLogger(DefaultLoggerReflectionTools.Logger);

        // Patches
        {
            HarmonyLog.Reset(Path.Combine(TemporaryDataPath, "harmony.log"));
            _patches = new uTest.Patches.UnturnedTestPatches(Logger);
            _patches.Init(
                TemporaryDataPath,
                p =>
                {
                    p.RegisterPatch(SkipAddFoundAssetIfNotRequired.TryPatch, SkipAddFoundAssetIfNotRequired.TryUnpatch);
                    p.RegisterPatch(IgnoreSocketExceptionsOnClient.TryPatch, IgnoreSocketExceptionsOnClient.TryUnpatch);
                    p.RegisterPatch(DisableConvenientSavedata.TryPatch, DisableConvenientSavedata.TryUnpatch);
                    //p.RegisterPatch(uTest.Patches.SocketMessageLayerFix.TryPatchClient, uTest.Patches.SocketMessageLayerFix.TryUnpatchClient);
                    p.RegisterPatch(ChangeHardwareIDs.TryPatch, ChangeHardwareIDs.TryUnpatch);
                    p.RegisterPatch(ReadyToConnectOverride.TryPatch, ReadyToConnectOverride.TryUnpatch);
                }
            );
        }

        if (File.Exists(LaunchConfig.AssetConfig))
        {
            using JsonTextReader reader = new JsonTextReader(new StreamReader(LaunchConfig.AssetConfig!, Encoding.UTF8, false))
            {
                CloseInput = true
            };

            if (AssetLoadModel.TryReadFromJson(reader, out AssetLoadModel? alm))
            {
                AssetLoadModel = alm;
            }
            else
            {
                Logger.LogError($"Failed to read JSON from asset load config at \"{LaunchConfig.AssetConfig}\".");
            }
        }

        ConvenientSavedata.instance = new ConvenientSavedataImplementation();

        _rpcEndpoint = NamedPipeEndpoint.AsClient(serviceProvider, LaunchConfig.PipeName);

        CommandWindow.Log($"Connecting to uTest server via Named Pipes as {steamId}...");

        Task<NamedPipeClientsideRemoteRpcConnection> connectTask = _rpcEndpoint.RequestConnectionAsync(TimeSpan.FromSeconds(15d));
        if (!connectTask.Wait(TimeSpan.FromSeconds(16d)))
            throw new TimeoutException("Timed out connecting.");
        _rpcConnection = connectTask.Result;

        CommandWindow.Log("Connected.");

        LocalWorkshopSettings.instance = new DummyLocalWorkshopSettings(this);

        Provider._client = SteamId;
        Provider._clientName = LaunchConfig.Name ?? SteamId.ToString();
        Provider._user = SteamId;

        DummyProvider.ShutdownOldProviderServices(Provider.provider);
        Provider.provider = new DummyProvider(this);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowHandle = RemoteDummyWindowsManager.GetWindowHandle(false);
            Logger.LogDebug($"HWND: {(long)WindowHandle:X16} | HMONITOR: {LaunchConfig.DisplayHandle.GetValueOrDefault():X16}");

            ReadOnlySpan<char> prefix = "uTest Simulated Client (";
            Span<char> windowTitle = stackalloc char[prefix.Length + 10];
            prefix.CopyTo(windowTitle);
            SteamId.GetAccountID().m_AccountID.TryFormat(windowTitle.Slice(prefix.Length, 8), out _, "X8", CultureInfo.InvariantCulture);
            windowTitle[^2] = ')';

            RemoteDummyWindowsManager.SetWindowTitle(WindowHandle, windowTitle);
        }

        if (GraphicsSettings.fullscreenMode != FullScreenMode.Windowed)
        {
            GraphicsSettings.fullscreenMode = FullScreenMode.Windowed;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && LaunchConfig.DisplayHandle.HasValue
            && WindowHandle != 0)
        {
            TimeUtility.updated += AlignWindowToGridOnFirstFrame;
        }

        TimeUtility.updated += UpdateSettingsOnFirstFrame;

        // apply is ran on MenuStartup.Start() anyways
        // GraphicsSettings.apply("Lower options for test clients.");

        StartStatusNotificationUpdate(DummyReadyStatus.StartedUp);

        Provider.onQueuePositionUpdated += HandleQueuePositionUpdated;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void UpdateSettingsOnFirstFrame()
    {
        if (GraphicsSettings.fullscreenMode != FullScreenMode.Windowed)
        {
            GraphicsSettings.fullscreenMode = FullScreenMode.Windowed;
        }

        GraphicsSettings.TargetFrameRate = 30;
        GraphicsSettings.effectQuality = EGraphicQuality.LOW;
        GraphicsSettings.foliageQuality = EGraphicQuality.LOW;
        GraphicsSettings.landmarkQuality = EGraphicQuality.LOW;
        GraphicsSettings.lightingQuality = EGraphicQuality.LOW;
        GraphicsSettings.outlineQuality = EGraphicQuality.LOW;
        GraphicsSettings.planarReflectionQuality = EGraphicQuality.LOW;
        GraphicsSettings.reflectionQuality = EGraphicQuality.LOW;
        GraphicsSettings.scopeQuality = EGraphicQuality.LOW;
        GraphicsSettings.sunShaftsQuality = EGraphicQuality.LOW;
        GraphicsSettings.terrainQuality = EGraphicQuality.LOW;
        GraphicsSettings.waterQuality = EGraphicQuality.LOW;
        GraphicsSettings.IsClutterEnabled = false;
        GraphicsSettings.IsItemIconAntiAliasingEnabled = false;
        GraphicsSettings.IsWindEnabled = false;
        GraphicsSettings.anisotropicFilteringMode = EAnisotropicFilteringMode.DISABLED;
        GraphicsSettings.antiAliasingType = EAntiAliasingType.OFF;
        GraphicsSettings.blast = false;
        GraphicsSettings.blend = false;
        GraphicsSettings.filmGrain = false;
        GraphicsSettings.buffer = false;
        GraphicsSettings.bloom = false;
        GraphicsSettings.debris = false;
        GraphicsSettings.chromaticAberration = false;
        GraphicsSettings.foliageFocus = false;
        GraphicsSettings.puddle = false;
        GraphicsSettings.UseUnfocusedTargetFrameRate = false;
        GraphicsSettings.glitter = false;
        GraphicsSettings.normalizedDrawDistance = 0;
        GraphicsSettings.normalizedLandmarkDrawDistance = 0;
        GraphicsSettings.renderMode = ERenderMode.FORWARD;
        GraphicsSettings.grassDisplacement = false;
        GraphicsSettings.ragdolls = false;
        GraphicsSettings.userInterfaceScale = 0.5f;
        GraphicsSettings.triplanar = false;
        GraphicsSettings.isAmbientOcclusionEnabled = false;
        GraphicsSettings.apply("uTest default settings.");

        OptionsSettings.UnfocusedVolume = 0f;
        OptionsSettings.MusicMasterVolume = 0f;
        OptionsSettings.MainMenuMusicVolume = 0f;
        TimeUtility.updated -= UpdateSettingsOnFirstFrame;
    }

    private int _delayedTicks;
    private void AlignWindowToGridOnFirstFrame()
    {
        ++_delayedTicks;
        if (_delayedTicks <= 1)
            return;

        // only subscribed on Windows

        TimeUtility.updated -= AlignWindowToGridOnFirstFrame;

        if (RemoteDummyWindowsManager.AlignWindowToGrid(
                (nint)LaunchConfig.DisplayHandle.GetValueOrDefault(),
                WindowHandle,
                LaunchConfig.Index + LaunchConfig.TileOffset,
                LaunchConfig.Count + LaunchConfig.TileOffset,
                out bool isPrimaryMonitor
            )
        )
        {
            Logger.LogTrace(isPrimaryMonitor
                ? $"Tiled game window (HWND 0x{(long)WindowHandle:X16}) to primary monitor."
                : $"Tiled game window (HWND 0x{(long)WindowHandle:X16}).");
        }
        else
            Logger.LogWarning("Failed to tile game window.");
    }

    private void StartStatusNotificationUpdate(DummyReadyStatus status)
    {
        try
        {
            Task.Run(async () =>
            {
                await SendStatusNotification(SteamId.m_SteamID, status, WindowHandle);
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

        if (Provider.connectionFailureInfo == ESteamConnectionFailureInfo.NONE)
        {
            StartStatusNotificationUpdate(DummyReadyStatus.InMenu);
            return;
        }

        try
        {
            Task.Run(async () =>
            {
                await SendRejectedStatusNotification(SteamId.m_SteamID, Provider.connectionFailureInfo, Provider.connectionFailureReason, Provider.connectionFailureDuration);
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

    private void HandleQueuePositionUpdated()
    {
        try
        {
            SendInQueueBump(SteamId.m_SteamID);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error bumping queue position.", ex);
        }
    }

    [RpcSend(typeof(RemoteDummyManager), "ReceiveInQueueBump")]
    private partial void SendInQueueBump(ulong steam64);

    [RpcSend(typeof(RemoteDummyManager), "ReceiveStatusNotification")]
    [RpcTimeout(14 * Timeouts.Seconds)]
    private partial RpcTask SendStatusNotification(ulong id, DummyReadyStatus status, nint hWnd);

    [RpcSend(typeof(RemoteDummyManager), "ReceiveRejectedStatusNotification")]
    private partial RpcTask SendRejectedStatusNotification(ulong id, ESteamConnectionFailureInfo rejection, string? reason, uint duration);

    [RpcReceive]
    private void ReceiveWorkshopItemsUpdate(ulong[] workshopItems)
    {
        GameThread.Run(workshopItems, workshopItems =>
        {
            WorkshopItems = workshopItems;
        });
    }

    [RpcReceive]
    private async Task ReceiveConnect(
        uint ipv4,
        ushort port,
        string? password,
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
        string descText,
        byte[]? hwids,
        string characterName,
        string nickName,
        byte characterIndex,
        ulong shirt,
        ulong pants,
        ulong hat,
        ulong backpack,
        ulong vest,
        ulong mask,
        ulong glasses,
        ulong group,
        ulong lobby,
        byte face,
        byte hair,
        byte beard,
        Color32 skinColor,
        Color32 hairColor,
        Color32 markerColor,
        Color32 beardColor,
        EClientPlatform platform,
        ushort reportedPing,
        bool isLeftHanded,
        ulong[]? activeSkins,
        EPlayerSkillset skillset,
        string? requiredModulesString,
        string language,
        uint? gameVersion,
        uint? mapVersion,
        bool correctGameVersion,
        bool correctMapVersion,
        bool correctLevelHash,
        bool correctAssemblyHash,
        bool correctResourceHash,
        bool correctEconHash
    )
    {
        await GameThread.Switch();

        IPv4Address addr = new IPv4Address(ipv4);
        Logger.LogInformation($"Received connection request to {addr}:{port} (ID {serverCode}).");

        if (characterIndex >= Characters.list.Length)
        {
            Logger.LogWarning($"Out of range character index: {characterIndex}.");
            characterIndex = 0;
        }

        Characters.selected = characterIndex;

        Character character = Characters.active;
        character.name = characterName;
        character.nick = nickName;
        character.group = new CSteamID(group);
        character.face = face;
        character.hair = hair;
        character.beard = beard;
        character.skin = skinColor;
        character.color = hairColor;
        character.markerColor = markerColor;
        character.BeardColor = beardColor;
        character.hand = isLeftHanded;

        // prevents steam from querying for TempSteamworksEconomy.wearingResult
        character.packageShirt = 0ul;
        character.packagePants = 0ul;
        character.packageHat = 0ul;
        character.packageBackpack = 0ul;
        character.packageVest = 0ul;
        character.packageMask = 0ul;
        character.packageGlasses = 0ul;
        Characters.packageSkins.Clear();

        character.skillset = skillset;

        HWIDs = hwids;

        ReadyToConnectInfo = ReadyToConnectInfo.Default;

        if (gameVersion.HasValue)
            ReadyToConnectInfo.GameVersion = gameVersion.Value;
        else if (!correctGameVersion)
            --ReadyToConnectInfo.GameVersion;

        ReadyToConnectInfo.Modules = requiredModulesString;
        ReadyToConnectInfo.Platform = platform;
        ReadyToConnectInfo.Language = language;
        ReadyToConnectInfo.IsGold = isPro;
        ReadyToConnectInfo.LobbyId = new CSteamID(lobby);
        ReadyToConnectInfo.ReportedPing = reportedPing;
        ReadyToConnectInfo.MixupLevelHash = !correctLevelHash;
        ReadyToConnectInfo.MixupAssemblyHash = !correctAssemblyHash;
        ReadyToConnectInfo.MixupResourceHash = !correctResourceHash;
        ReadyToConnectInfo.MixupEconHash = !correctEconHash;
        ReadyToConnectInfo.OverrideLevelVersion = mapVersion;
        ReadyToConnectInfo.MixupLevelVersion = !correctMapVersion;

        Provider.connect(
            new ServerConnectParameters(addr, (ushort)(port - 1u), port, password ?? string.Empty),
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

        Logger.LogInformation("Received graceful disconnect request from uTest server.");
        Provider.RequestDisconnect("Requested by uTest.");
    }

    [RpcReceive]
    private Task ReceiveGracefullyClose()
    {
        Logger.LogInformation("Received graceful shutdown request from uTest server.");
        return GameThread.RunAndWaitAsync(() => Provider.QuitGame("Tests completed."));
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        TimeUtility.updated -= GameThread.RunContinuations;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Provider.onQueuePositionUpdated -= HandleQueuePositionUpdated;
        _patches?.Dispose();
        _patches = null;
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
#if DEBUG
        UnturnedLog.info($"Command line: \"{System.Environment.CommandLine}\".");
#endif
        ITranspileContextLogger logger = null;
        try
        {
            // copy 0Harmony.dll v2.3.3 to the working directory
            // for some reason it refuses to work on any other version only on the client module, server module works fine
            //Assembly thisAsm = Assembly.GetExecutingAssembly();
            //string fileName = Path.Combine(Path.GetDirectoryName(thisAsm.Location)!, "0Harmony (2.3.3).exe");
            //DateTime srcLastModified = FileHelper.GetLastWriteTimeUTCSafe(thisAsm, DateTime.MaxValue);
            //DateTime dstLastModified = FileHelper.GetLastWriteTimeUTCSafe(fileName, DateTime.MinValue);
            //if (srcLastModified > dstLastModified)
            //{
            //    Stream? harmonyDll = thisAsm.GetManifestResourceStream("uTest.Dummies.Host.lib.0Harmony.dll");
            //    if (harmonyDll == null)
            //    {
            //        throw new Exception("Failed to find 0Harmony.dll lib in embedded resources.");
            //    }
            //
            //    using FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
            //    harmonyDll.CopyTo(fs);
            //}
            //
            //Assembly asm = Assembly.LoadFrom(fileName);
            //UnturnedLog.info($"Loaded {asm}");

            GameThread.Setup();
            Init();
        }
        catch (Exception ex)
        {
            UnturnedLog.error("Startup error.");
            UnturnedLog.error(ex);

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
            UnturnedLog.warn("uTest failed to find field 'Provider.wasQuitGameCalled'.");
        }

        UnturnedLog.info($"uTest Quit game: {reason}. Exit code: {(int)exitCode} ({exitCode}).");
        try
        {
            shutdown();
        }
        catch (Exception ex)
        {
            UnturnedLog.error("Shutdown error");
            UnturnedLog.error(ex);
        }
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}