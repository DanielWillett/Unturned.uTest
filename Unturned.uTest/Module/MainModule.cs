using DanielWillett.ReflectionTools;
using Newtonsoft.Json;
using SDG.Framework.IO;
using SDG.Framework.Modules;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Discovery;
using uTest.Dummies;
using uTest.Patches;
using uTest.Protocol;
using Component = UnityEngine.Component;

namespace uTest.Module;

/// <summary>
/// Main class for Unturned functionality of uTest.
/// </summary>
internal class MainModule : MonoBehaviour, IDisposable
{
    private const string TestFileCommandLine = "-uTestSettings";

    private bool _hasQuit;

    private bool _nextFrameLevelIsLoaded;
    private bool _hasReceivedRunTests;
    private bool _hasAssetLoadModel;
    private float _sentLevelLoadedRealtime;
    private Task? _discoverTestsTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private TaskCompletionSource<int>? _assetLoadModelTrigger;

    private readonly CommandLineString _clTestFile = new CommandLineString(TestFileCommandLine);

#nullable disable
    internal static MainModule Instance { get; private set; }

#nullable restore

    /// <summary>
    /// If startup ran into an error.
    /// </summary>
    public bool IsFaulted { get; private set; } = true;

    /// <summary>
    /// A token that will be triggered when this test run is cancelled.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// List of all tests to be ran.
    /// </summary>
    public UnturnedTestList? TestList { get; private set; }

    /// <summary>
    /// Server used to communicate with the test runner.
    /// </summary>
    public TestEnvironmentServer Environment { get; private set; } = null!;

    /// <summary>
    /// The Unturned logger.
    /// </summary>
    public ILogger Logger => DefaultLogger.Logger;

    /// <summary>
    /// The assembly that contains the running tests.
    /// </summary>
    public Assembly TestAssembly { get; private set; } = null!;
    
    /// <summary>
    /// The directory this module is stored in.
    /// </summary>
    public string HomeDirectory { get; private set; } = null!;

    /// <summary>
    /// Manages dummies for tests run on a server.
    /// </summary>
    public DummyManager Dummies { get; private set; } = null!;

    /// <summary>
    /// Keeps track of completed patches and unpatches.
    /// </summary>
    internal UnturnedTestPatches? Patches { get; private set; }

    /// <summary>
    /// List of all included workshop items.
    /// </summary>
    internal ulong[]? WorkshopItems { get; private set; }

    /// <summary>
    /// Defines which assets are loaded.
    /// </summary>
    /// <remarks>If this property is null it means all assets should be loaded.</remarks>
    public AssetLoadModel? AssetLoadModel
    {
        get
        {
            if (_hasAssetLoadModel)
                return field;

            _assetLoadModelTrigger?.Task.Wait();
            return field;
        }
        private set;
    }

    /// <summary>
    /// List of tests to be ran.
    /// </summary>
    public UnturnedTestInstanceData[] Tests { get; private set; } = Array.Empty<UnturnedTestInstanceData>();

    /// <summary>
    /// Entrypoint for module.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Initialize(string homeDir, SDG.Framework.Modules.Module module)
    {
        Accessor.Logger = DefaultLoggerReflectionTools.Logger;
        Accessor.LogDebugMessages = true;
        Accessor.LogInfoMessages = true;
        Accessor.LogWarningMessages = true;
        Accessor.LogErrorMessages = true;

        IsFaulted = false;
        HomeDirectory = homeDir;
        Instance = this;
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken = _cancellationTokenSource.Token;

        bool failedToParse = false;
        if (!_clTestFile.hasValue || !TryRefreshTestFile(out failedToParse))
        {
            CommandWindow.LogError(
                !failedToParse
                    ? $"""Test file not provided or unable to be read. Unturned must be launched with command line: {TestFileCommandLine} "File\Path\To\Tests.yml"."""
                    : "Test file failed to parse valid JSON."
            );
            IsFaulted = true;
            return;
        }

        Provider.onServerConnected += HandlePlayerConnected;

        Version version = Assembly.GetExecutingAssembly().GetName().Version;

        TestAssembly = Array.Find(module.assemblies, x => string.Equals(x.FullName, TestList.TestAssembly));
        if (TestAssembly == null)
        {
            CommandWindow.LogError("Failed to find test assembly.");
            IsFaulted = true;
            return;
        }

        Dedicator.commandWindow.title = Properties.Resources.WindowTitle;

        // todo: docs
        string log = $"""
                      Launching uTest v{version} on Unturned v{Provider.APP_VERSION} by DanielWillett aka BlazingFlame (@danielwillett on Discord).
                      - GitHub            : https://github.com/DanielWillett/Unturned.uTest
                      - Docs              : 
                      - Report a problem  : https://github.com/DanielWillett/Unturned.uTest/issues
                      - Request a feature : https://github.com/DanielWillett/Unturned.uTest/discussions
                      = uTest is licensed under GPL-3.0
                      |  Unturned.uTest  Copyright (C) {DateTime.Now.Year}  Daniel Willett
                      |  This program comes with ABSOLUTELY NO WARRANTY.
                      |  This is free software, and you are welcome to redistribute it
                      |  under certain conditions.
                      = View full license at https://github.com/DanielWillett/Unturned.uTest/blob/master/LICENSE.txt.
                      """;

        CommandWindow.Log(log);

        // Patches
        {
            Patches = new UnturnedTestPatches(Logger);
            Patches.Init(
                Path.Combine(ReadWrite.PATH, "Logs"),
                p =>
                {
                    p.RegisterPatch(SkipAddFoundAssetIfNotRequired.TryPatch, SkipAddFoundAssetIfNotRequired.TryUnpatch);
                }
            );
        }

        Dummies = new DummyManager(this);
        Dummies.ClearPlayerDataFromDummies();

        Task t = DiscoverTestsAsync(TestList);
        if (!t.IsCompleted)
        {
            _discoverTestsTask = t;
        }
        else
        {
            // throw any exceptions
            t.GetAwaiter().GetResult();
        }

        Provider.configData.Server.Max_Clients_With_Same_IP_Address = int.MaxValue;

        Level.onPostLevelLoaded += OnPostLevelLoaded;

        Environment = new TestEnvironmentServer(Logger);
        Environment.Disconnected += () =>
        {
            if (_hasQuit)
                return;

            Logger.LogWarning("Lost contact with runner, shutting down...");
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Shutdown due to losing contact with runner.", UnturnedTestExitCode.GracefulShutdown);
            });
        };

        Environment.AddMessageHandler<RefreshTestsMessage>(_ =>
        {
            TryRefreshTestFile(out bool _);
            return true;
        });

        Environment.AddMessageHandler<RunTestsMessage>(_ =>
        {
            _hasReceivedRunTests = true;
            TestRunner runner = new TestRunner(this);

            Task.Run(async () =>
            {
                UnturnedTestExitCode exitCode;
                try
                {
                    Task? t = _discoverTestsTask;
                    if (t != null)
                    {
                        await t;
                        _discoverTestsTask = null;
                    }

                    exitCode = await runner.RunTestsAsync(CancellationToken);
                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    await Logger.LogErrorAsync("Error running tests.", ex);
                    exitCode = UnturnedTestExitCode.StartupFailure;
                }

                GameThread.Run(
                    exitCode,
                    exitCode => ForceQuitGame("Test run completed.", exitCode)
                );
            }, CancellationToken);

            return true;
        });

        Environment.AddMessageHandler<GracefulShutdownMessage>(_ =>
        {
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Graceful shutdown from uTest.", UnturnedTestExitCode.GracefulShutdown);
            });
            return true;
        });
    }

    private void HandlePlayerConnected(CSteamID steamID)
    {
        Player player = PlayerTool.getPlayer(steamID);
        if (player == null)
        {
            Logger.LogError($"Unable to find corresponding SDG.Unturned.Player for {steamID}.");
            return;
        }

        if (Dummies.TryGetDummy(steamID.m_SteamID, out BaseServersidePlayerActor? actor))
        {
            Logger.LogInformation($"Dummy connected: {steamID}.");
            actor.NotifyConnected(player);
        }
        else
        {
            Logger.LogInformation($"Real player connected: {steamID}.");
        }
    }

    [MemberNotNull(nameof(Tests))]
    private async Task DiscoverTestsAsync(UnturnedTestList testList)
    {
        _assetLoadModelTrigger = new TaskCompletionSource<int>();
        if (testList.Tests == null || testList.Tests.Count == 0)
        {
            Tests = Array.Empty<UnturnedTestInstanceData>();
            return;
        }

        Type listType = Type.GetType(testList.TestListTypeName, throwOnError: true, ignoreCase: false)!;

        ITestRegistrationList list = (ITestRegistrationList)Activator.CreateInstance(listType, TestAssembly);

        ITestFilter? filter = null;
        if (!testList.IsAllTests)
        {
            string[] uids = new string[testList.Tests.Count];
            for (int i = 0; i < testList.Tests.Count; ++i)
                uids[i] = testList.Tests[i].Uid;

            filter = new UidListFilter(uids);
        }

        if (!string.IsNullOrEmpty(testList.TreeNodeFilter))
        {
            Logger.LogDebug($"Test filter: \"{testList.TreeNodeFilter}\"");
        }

        Logger.LogInformation($"Discovering tests in \"{TestAssembly.GetName().FullName}\" ...");

        List<UnturnedTestInstance> tests = await list.GetMatchingTestsAsync(Logger, filter, CancellationToken).ConfigureAwait(false);

        Logger.LogInformation($"Found {tests.Count} test(s).");

        UnturnedTestInstanceData[] testArray = new UnturnedTestInstanceData[tests.Count];
        for (int i = 0; i < testArray.Length; ++i)
            testArray[i] = new UnturnedTestInstanceData(tests[i]);

        HashSet<ulong> workshopItems = new HashSet<ulong>();
        foreach (UnturnedTestInstanceData data in testArray)
        {
            foreach (ulong workshopItem in data.Instance.Test.WorkshopItems)
            {
                workshopItems.Add(workshopItem);
            }
        }

        WorkshopItems = workshopItems.ToArray();

        // todo: doesnt work on client and is a race condition with asset loading (see Provider.host())
        await GameThread.RunAndWaitAsync(workshopItems, static workshopItems =>
        {
            WorkshopDownloadConfig.getOrLoad().File_IDs = new List<ulong>(workshopItems);
        }, CancellationToken);

        AssetLoadModel = AssetLoadModel.Create(this, true);
        _hasAssetLoadModel = true;
        _assetLoadModelTrigger?.TrySetResult(0);

        if (Dedicator.isStandaloneDedicatedServer)
        {
            if (!await Dummies.InitializeDummiesAsync(testArray))
            {
                throw new Exception("Failed to initialize dummies.");
            }
        }
        
        Tests = testArray;

        await Environment.SendAsync(new AllInstancesStartedMessage(), CancellationToken);
        _discoverTestsTask = null;
    }

    private void OnPostLevelLoaded(int level)
    {
        Level.onPostLevelLoaded -= OnPostLevelLoaded;
        _nextFrameLevelIsLoaded = true;
    }

    /// <summary>
    /// Ran one frame after level loaded event so <see cref="Level.isLoaded"/> is <see langword="true"/>.
    /// </summary>
    private void OnLevelLoaded()
    {
        Task.Run(async () =>
        {
            await Environment.SendAsync(new LevelLoadedMessage(), CancellationToken);
            await Logger.LogInformationAsync("Sent level loaded");
        }, CancellationToken);

        _sentLevelLoadedRealtime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (_nextFrameLevelIsLoaded)
        {
            _nextFrameLevelIsLoaded = false;
            OnLevelLoaded();
        }

        GameThread.RunContinuations();

        if (!_hasReceivedRunTests && _sentLevelLoadedRealtime - Time.realtimeSinceStartup > 2)
        {
            ForceQuitGame("Timed out waiting for RunTests message from runner.", UnturnedTestExitCode.StartupFailure);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch { /* ignored */ }

        Instance = null;

        if (_hasQuit)
            return;

        Patches?.Dispose();
        Patches = null;

        Dummies?.Dispose();

        Provider.onServerConnected -= HandlePlayerConnected;
        GameThread.FlushRunAndWaits();

        _hasQuit = true;
        IsFaulted = false;
        Environment?.Dispose();
        Environment = null!;
    }

    [MemberNotNullWhen(true, nameof(TestList))]
    public bool TryRefreshTestFile(out bool failedToParse)
    {
        failedToParse = false;

        string path = _clTestFile.value;
        try
        {
            UnturnedTestList? list = IOUtility.jsonDeserializer.deserialize<UnturnedTestList>(path);
            if (list == null)
            {
                failedToParse = true;
                return false;
            }

            list.Tests ??= new List<UnturnedTestReference>(0);

            CommandWindow.Log($"Test session: \"{list.SessionUid}\". {list.Tests.Count} test(s).");

            TestList = list;
        }
        catch (JsonException ex)
        {
            // bad JSON
            failedToParse = true;
            CommandWindow.LogError(ex);
            return false;
        }
        catch (Exception ex)
        {
            // file not found, etc
            CommandWindow.LogError(ex);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Quits the game instantly with an exit code.
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    [DoesNotReturn]
#endif
    public void ForceQuitGame(string reason, UnturnedTestExitCode exitCode)
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
        Dispose();
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}

/// <summary>
/// The entrypoint for uTest when loaded as an Unturned module.
/// </summary>
internal class MainModuleLoader : IModuleNexus
{
    private object? _module;
    private static readonly CommandLineFlag? IsDummyFlag = new CommandLineFlag(false, "-uTestSteamId");
    internal static string? _homeDir;
    internal static SDG.Framework.Modules.Module? _sdgModule;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.initialize()
    {
        if (IsDummyFlag!.value)
            return;
        
        GameThread.Setup();
        _sdgModule = ModuleHook.getModuleByName("uTest");
        if (_sdgModule?.config.DirectoryPath == null)
        {
            _homeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        else
        {
            _homeDir = _sdgModule.config.DirectoryPath;
        }

        if (_sdgModule == null || !Directory.Exists(_homeDir))
        {
            ForceQuitGame("uTest initialization failed. Failed to find uTest module.", UnturnedTestExitCode.StartupFailure);
            return;
        }

        Init();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Init()
    {
        GameObject go = new GameObject("uTest");
        Object.DontDestroyOnLoad(go);
        
        MainModule module = go.AddComponent<MainModule>();
        _module = module;

        try
        {
            module.Initialize(_homeDir!, _sdgModule!);
            if (module.IsFaulted)
            {
                module.ForceQuitGame("uTest initialization failed. See log.", UnturnedTestExitCode.StartupFailure);
            }
        }
        catch (QuitGameException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            module.ForceQuitGame("Exception thrown during uTest initialization. See log.", UnturnedTestExitCode.StartupFailure);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.shutdown()
    {
        if (_module is IDisposable disposable)
            disposable.Dispose();

        if (_module is Component comp)
            Object.Destroy(comp.gameObject);
    }

    private static void ForceQuitGame(string reason, UnturnedTestExitCode exitCode)
    {
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
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}