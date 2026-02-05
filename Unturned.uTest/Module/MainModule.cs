using DanielWillett.ReflectionTools;
using Newtonsoft.Json;
using SDG.Framework.IO;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Compat;
using uTest.Compat.DependencyInjection;
using uTest.Compat.Lifetime;
using uTest.Compat.Logging;
using uTest.Discovery;
using uTest.Dummies;
using uTest.Patches;
using uTest.Protocol;

namespace uTest.Module;

/// <summary>
/// Main class for Unturned functionality of uTest.
/// </summary>
internal class MainModule : MonoBehaviour, IDisposable, IUnturnedTestRuntime
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
    /// The exception formatter to use when printing exceptions to your IDE.
    /// </summary>
    /// <remarks>If the <c>DanielWillett.StackCleaner</c> package is installed, this will be <see cref="StackCleanerExceptionFormatter"/> by default, otherwise it will be <see langword="null"/> which is just <see cref="Exception.ToString"/>.</remarks>
    public IExceptionFormatter? ExceptionFormatter { get; set; }

    /// <summary>
    /// Server used to communicate with the test runner.
    /// </summary>
    public TestEnvironmentServer Environment { get; private set; } = null!;

    /// <summary>
    /// The Unturned logger.
    /// </summary>
    public ILogger Logger { get; private set; } = DefaultLogger.Logger;

    /// <summary>
    /// The highest-priority logger integration currently being used from the available modules.
    /// </summary>
    public ILoggerIntegration? LoggerIntegration { get; private set; }

    /// <summary>
    /// The highest-priority test runner activator currently being used from the available modules.
    /// </summary>
    public ITestRunnerActivator? TestRunnerActivator { get; private set; }

    /// <summary>
    /// Sorted list of implementations of <see cref="ITestLifetimeIntegration"/>.
    /// </summary>
    public ITestLifetimeIntegration[]? TestLifetimeIntegrations { get; private set; }

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
        Accessor.LogDebugMessages = true;
        Accessor.LogInfoMessages = true;
        Accessor.LogWarningMessages = true;
        Accessor.LogErrorMessages = true;

        LoadLoggerIntegration();
        
        if (LoggerIntegration != null)
        {
            Accessor.Logger = new ReflectionToolsLoggerWrapper(
                LoggerIntegration.CreateNamedLogger("DanielWillett.ReflectionTools")
            );
        }
        else
        {
            Accessor.Logger = DefaultLoggerReflectionTools.Logger;
        }

        IsFaulted = false;
        HomeDirectory = homeDir;
        Instance = this;
        UnturnedTestHost.SetRuntime(this);
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken = _cancellationTokenSource.Token;

        bool failedToParse = false;
        if (!_clTestFile.hasValue || !TryRefreshTestFile(out failedToParse))
        {
            Logger.LogError(
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
            Logger.LogError("Failed to find test assembly.");
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

        Logger.LogInformation(log);

        Logger.LogInformation("If you see a dependency error below this about StackCleaner it can be ignored.");
        ExceptionFormatter ??= StackCleanerExceptionFormatter.GetStackCleanerFormatterIfInstalled(TestList.UseColorfulStackTrace);


        // Patches
        {
            Patches = new UnturnedTestPatches(Logger);
            Patches.Init(
                Path.Combine(ReadWrite.PATH, "Logs"),
                p =>
                {
                    p.RegisterPatch(SkipAddFoundAssetIfNotRequired.TryPatch, SkipAddFoundAssetIfNotRequired.TryUnpatch);
                    //p.RegisterPatch(SocketMessageLayerFix.TryPatchServer, SocketMessageLayerFix.TryUnpatchServer);
                    //if (!Dedicator.isStandaloneDedicatedServer)
                    //    p.RegisterPatch(SocketMessageLayerFix.TryPatchClient, SocketMessageLayerFix.TryUnpatchClient);
                }
            );
        }

        Dummies = new DummyManager(this);
        Dummies.ClearPlayerDataFromDummies();

        Environment = new TestEnvironmentServer(Logger);

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

        Environment.Disconnected += () =>
        {
            if (_hasQuit)
                return;

            Logger.LogWarning("Lost contact with runner, shutting down...");
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Shutdown due to losing contact with runner", UnturnedTestExitCode.GracefulShutdown);
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
                    exitCode => ForceQuitGame("Test run completed", exitCode)
                );
            }, CancellationToken);

            return true;
        });

        Environment.AddMessageHandler<GracefulShutdownMessage>(_ =>
        {
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Graceful shutdown from uTest", UnturnedTestExitCode.GracefulShutdown);
            });
            return true;
        });
    }

    internal ILogger GetOrCreateLogger(string name)
    {
        ILoggerIntegration? integration = LoggerIntegration;
        return integration != null ? integration.CreateNamedLogger(name) : Logger;
    }

    /// <inheritdoc />
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
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

        await GameThread.Switch();

        Logger.LogInformation($"Found {tests.Count} test(s).");

        UnturnedTestInstanceData[] testArray = new UnturnedTestInstanceData[tests.Count];
        for (int i = 0; i < testArray.Length; ++i)
        {
            testArray[i] = new UnturnedTestInstanceData(tests[i], testList.SessionUid);
        }

        HashSet<ulong> workshopItems = new HashSet<ulong>();
        foreach (UnturnedTestInstanceData data in testArray)
        {
            foreach (ulong workshopItem in data.Instance.Test.WorkshopItems)
            {
                workshopItems.Add(workshopItem);
            }
        }

        WorkshopItems = workshopItems.ToArray();

        GameThread.Assert();
        // todo: doesnt work on client and is a race condition with asset loading (see Provider.host())
        WorkshopDownloadConfig.getOrLoad().File_IDs = new List<ulong>(workshopItems);

        if (WorkshopItems.Length > 0)
        {
            Logger.LogInformation($"Workshop items: {string.Join(", ", WorkshopDownloadConfig.getOrLoad().File_IDs)}.");
        }

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
    /// This also gives OpenMod time to invoke it's <c>UnturnedPostLevelLoadedEvent</c> and other level events.
    /// Additionally, rocket plugins subscribing to any of the level load events will have time to run their code as well.
    /// </summary>
    private void OnLevelLoaded()
    {
        Task.Run(async () =>
        {
            // this needs to happen after openmod has had time to load, in case uTest loads before OpenMod
            LoadActivatorIntegration();
            LoadTestLifetimeIntegrations();

            await InvokeStartupHookTypes().ConfigureAwait(false);

            await Environment.SendAsync(new LevelLoadedMessage(), CancellationToken);
            await Logger.LogInformationAsync("Sent level loaded");
            await GameThread.Switch(CancellationToken);
            _sentLevelLoadedRealtime = Time.realtimeSinceStartup;
        }, CancellationToken);
    }

    private async Task InvokeStartupHookTypes()
    {
        HashSet<Type> startupHookTypes = new HashSet<Type>();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            IEnumerable<Attribute> startupHooks;
            try
            {
                startupHooks = assembly.GetCustomAttributes(typeof(RegisterStartupHookAttribute));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to fetch startup hooks for assembly {assembly.FullName}.{
                    System.Environment.NewLine}{ExceptionFormatter?.FormatException(ex) ?? ex.ToString()}."
                );
                continue;
            }

            foreach (RegisterStartupHookAttribute startupHook in startupHooks.Cast<RegisterStartupHookAttribute>())
            {
                if (startupHook.Type == null)
                    continue;

                startupHookTypes.Add(startupHook.Type);
            }
        }

        Queue<Type> queue = new Queue<Type>(startupHookTypes);
        Queue<IStartupHook> hookQueue = new Queue<IStartupHook>();
        List<IStartupHook> hooks = new List<IStartupHook>(startupHookTypes.Count);
        try
        {
            while (hookQueue.Count > 0 || queue.Count > 0)
            {
                IStartupHook? hook;
                if (hookQueue.Count > 0)
                {
                    hook = hookQueue.Dequeue();
                    hooks.Add(hook);
                }
                else
                {
                    Type type = queue.Dequeue();
                    if (!typeof(IStartupHook).IsAssignableFrom(type))
                    {
                        Logger.LogError(
                            $"Assembly {type.Assembly.GetName().FullName} defines type {type.FullName} " +
                            $"as a startup hook but it doesn't implement IStartupHook."
                        );
                        continue;
                    }

                    hook = hooks.Find(x => type.IsInstanceOfType(x));
                    if (hook == null)
                    {
                        ConstructorInfo? ctor = type.GetConstructor([ typeof(ILogger) ]);
                        object[] parameters;
                        if (ctor != null)
                        {
                            parameters = [ Logger ];
                        }
                        else
                        {
                            parameters = Array.Empty<object>();
                            ctor = type.GetConstructor(Type.EmptyTypes);
                        }

                        if (ctor == null)
                        {
                            Logger.LogError(
                                $"Startup hook {type.AssemblyQualifiedName} must define either a parameterless constructor " +
                                $"or a constructor with a single {typeof(ILogger).FullName} parameter."
                            );
                            continue;
                        }

                        try
                        {
                            hook = (IStartupHook)ctor.Invoke(parameters);
                            hooks.Add(hook);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Constructor for startup hook {type.AssemblyQualifiedName} threw an exception.", ex);
                            continue;
                        }
                    }
                }

                IList<StartupHook> types;
                try
                {
                    types = await hook.WaitAsync(CancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Implementation for startup hook {hook.GetType().AssemblyQualifiedName} threw an exception.", ex);
                    continue;
                }

                foreach (StartupHook hookInstance in types)
                {
                    if (hookInstance.Type != null)
                    {
                        queue.Enqueue(hookInstance.Type);
                    }
                    else if (hookInstance.Hook != null)
                    {
                        hookQueue.Enqueue(hookInstance.Hook);
                    }
                }
            }
        }
        finally
        {
            foreach (IDisposable hook in hooks.OfType<IDisposable>())
            {
                try
                {
                    hook.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Startup hook {hook.GetType().AssemblyQualifiedName} threw an exception when disposing.", ex);
                }
            }
        }
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
            ForceQuitGame("Timed out waiting for RunTests message from runner", UnturnedTestExitCode.StartupFailure);
        }
    }

    private void LoadLoggerIntegration()
    {
        List<ILoggerIntegration> integrations = new List<ILoggerIntegration>(0);

        if (CompatibilityInformation.IsOpenModInstalled)
        {
            try
            {
                ILoggerIntegration openMod = LoggingIntegrations.TryInstallOpenModLoggingIntegration(Logger);

                integrations.Add(openMod);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load OpenMod logging compatibility.", ex);
            }
        }

        if (integrations.Count <= 0)
            return;

        integrations.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        ILoggerIntegration integration = integrations[0];
        for (int i = 1; i < integrations.Count; ++i)
        {
            if (integrations[i] is IDisposable disp)
                disp.Dispose();
        }

        LoggerIntegration = integration;
        Logger = integration.CreateNamedLogger("uTest");
    }

    private void LoadActivatorIntegration()
    {
        List<ITestRunnerActivator> integrations = new List<ITestRunnerActivator>(0);

        if (CompatibilityInformation.IsOpenModInstalled)
        {
            try
            {
                ITestRunnerActivator openMod = TestRunnerActivatorIntegrations.TryInstallOpenModTestRunnerActivator(Logger);

                integrations.Add(openMod);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load OpenMod service injection compatibility.", ex);
            }
        }

        if (integrations.Count <= 0)
            return;

        integrations.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        ITestRunnerActivator integration = integrations[0];
        for (int i = 1; i < integrations.Count; ++i)
        {
            if (integrations[i] is IDisposable disp)
                disp.Dispose();
        }

        TestRunnerActivator = integration;
    }

    private void LoadTestLifetimeIntegrations()
    {
        List<ITestLifetimeIntegration> integrations = new List<ITestLifetimeIntegration>(0);

        if (CompatibilityInformation.IsOpenModInstalled)
        {
            try
            {
                ITestLifetimeIntegration openMod = Compat.TestLifetimeIntegrations.TryInstallOpenModTestLifetime(Logger);

                integrations.Add(openMod);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load OpenMod event and player compatibility.", ex);
            }
        }

        if (integrations.Count <= 0)
            return;

        integrations.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        TestLifetimeIntegrations = integrations.ToArray();
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

        ITestRunnerActivator? runnerActivator = TestRunnerActivator;
        TestRunnerActivator = null;
        if (runnerActivator is IDisposable disp1)
            disp1.Dispose();

        Instance = null;
        UnturnedTestHost.SetRuntime(null);

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

        ILoggerIntegration? integration = LoggerIntegration;
        LoggerIntegration = null;
        if (integration is IDisposable disp2)
            disp2.Dispose();
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

            Logger.LogInformation($"Test session: \"{list.SessionUid}\". {list.Tests.Count} test(s).");

            TestList = list;
        }
        catch (JsonException ex)
        {
            // bad JSON
            failedToParse = true;
            Logger.LogError(ex);
            return false;
        }
        catch (Exception ex)
        {
            // file not found, etc
            Logger.LogError(ex);
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
            Logger.LogWarning("uTest failed to find field 'Provider.wasQuitGameCalled'.");
        }

        UnturnedLog.info($"uTest Quit game: {reason}. Exit code: {(int)exitCode} ({exitCode}).");
        Dispose();
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}