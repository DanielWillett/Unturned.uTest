using HarmonyLib;
using Newtonsoft.Json;
using SDG.Framework.IO;
using SDG.Framework.Modules;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Discovery;
using uTest.Dummies;
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
    private Harmony? _harmony;
    private List<Func<Harmony, bool>> _unpatches = null!;

    private bool _nextFrameLevelIsLoaded;
    private bool _hasReceivedRunTests;
    private float _sentLevelLoadedRealtime;
    private Task? _discoverTestsTask;
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly CommandLineString _clTestFile = new CommandLineString(TestFileCommandLine);

#nullable disable
    internal static MainModule Instance { get; private set; }

#nullable restore

    /// <summary>
    /// If startup ran into an error.
    /// </summary>
    public bool IsFaulted { get; private set; }

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
    /// Defines which assets are loaded.
    /// </summary>
    /// <remarks>If this property is null it means all assets should be loaded.</remarks>
    public AssetLoadModel? AssetLoadModel
    {
        get
        {
            if (_discoverTestsTask == null)
                return field;

            lock (this)
            {
                _discoverTestsTask?.Wait();
                return field;
            }
        }
        private set;
    }

    /// <summary>
    /// List of tests to be ran.
    /// </summary>
    public UnturnedTestInstance[] Tests { get; private set; } = Array.Empty<UnturnedTestInstance>();

    /// <summary>
    /// Entrypoint for module.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Initialize()
    {
        Instance = this;
        _cancellationTokenSource = new CancellationTokenSource();

        GameThread.Setup();

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

        Version version = Assembly.GetExecutingAssembly().GetName().Version;

        SDG.Framework.Modules.Module? module = ModuleHook.getModuleByName("uTest");
        if (module == null)
        {
            CommandWindow.LogError("Failed to find uTest module.");
            IsFaulted = true;
            return;
        }

        HomeDirectory = Path.GetFullPath(module.config.DirectoryPath);

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
                      Launching uTest v{version} on Unturned v{Provider.APP_VERSION} by DanielWillett (@danielwillett on Discord).
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

        Dummies = new DummyManager(this);

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

        // Patches
        {
            _harmony = new Harmony("DanielWillett.uTest");
            _unpatches = new List<Func<Harmony, bool>>(8);

            RegisterPatch(Patches.SkipAddFoundAssetIfNotRequired.TryPatch, Patches.SkipAddFoundAssetIfNotRequired.TryUnpatch);
            RegisterPatch(Patches.ListenServerAddDummies.TryPatch, Patches.ListenServerAddDummies.TryUnpatch);
        }

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

        Level.onPostLevelLoaded += OnPostLevelLoaded;

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
                        await t;

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

    private void RegisterPatch(Func<Harmony, ILogger, bool> tryPatch, Func<Harmony, bool> tryUnpatch)
    {
        if (tryPatch(_harmony!, Logger))
            _unpatches.Add(tryUnpatch);
    }

    [MemberNotNull(nameof(Tests))]
    private async Task DiscoverTestsAsync(UnturnedTestList testList)
    {
        if (testList.Tests == null || testList.Tests.Count == 0)
        {
            Tests = Array.Empty<UnturnedTestInstance>();
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

        Logger.LogInformation($"Discovering tests in \"{TestAssembly.GetName().FullName}\" ...");

        List<UnturnedTestInstance> tests = await list.GetMatchingTestsAsync(Logger, filter, CancellationToken).ConfigureAwait(false);

        Logger.LogInformation($"Found {tests.Count} test(s).");

        UnturnedTestInstance[] testArray = tests.ToArray();

        if (Dedicator.isStandaloneDedicatedServer)
        {
            await Dummies.InitializeDummiesAsync(testArray);
        }

        lock (this)
        {
            Tests = testArray;

            AssetLoadModel = AssetLoadModel.Create(this, true);

            _discoverTestsTask = null;
        }
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

        if (_harmony != null)
        {
            foreach (Func<Harmony, bool> unpatch in _unpatches)
                unpatch(_harmony);

            _harmony.UnpatchAll(_harmony.Id);
            _harmony = null;
        }

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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.initialize()
    {
        try
        {
            Init();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            throw;
        }
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
            module.Initialize();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            if (!module.IsFaulted)
                throw;
        }
        finally
        {
            if (module.IsFaulted)
            {
                module.ForceQuitGame("uTest initialization failed. See log.", UnturnedTestExitCode.StartupFailure);
            }
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
}