using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Newtonsoft.Json;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using uTest.Discovery;
using uTest.Module;
using uTest.Protocol;
using uTest.Runner.Unturned;
using uTest.Runner.Util;
using LogLevel = uTest.Logging.LogLevel;

namespace uTest.Runner;


#pragma warning disable TPEXP
internal class UnturnedTestFramework : ITestFramework, IDisposable, IDataProducer
{
    internal const string ConfigurationPrefix = "uTest:";

    internal class GracefulStopCapability : IGracefulStopTestExecutionCapability
    {
        public Func<CancellationToken, Task>? InvokeExecution;

        /// <inheritdoc />
        public Task StopTestExecutionAsync(CancellationToken cancellationToken)
        {
            return InvokeExecution == null
                ? Task.CompletedTask
                : InvokeExecution(cancellationToken);
        }
    }

    private static readonly TestNodeStateProperty[] BasicTestResultStates =
    [
        new SkippedTestNodeStateProperty(uTest.Properties.Resources.TestResultInconclusive),
        new PassedTestNodeStateProperty(uTest.Properties.Resources.TestResultPass),
        new FailedTestNodeStateProperty(uTest.Properties.Resources.TestResultFail),
        new TimeoutTestNodeStateProperty(uTest.Properties.Resources.TestResultTimeout),
        new InProgressTestNodeStateProperty(uTest.Properties.Resources.TestResultInProgress),
        new SkippedTestNodeStateProperty(uTest.Properties.Resources.TestResultSkipped)
    ];

    private static readonly Func<string, TestNodeStateProperty>[] TestResultStateFactories =
    [
        // in order of TestResult fields
        msg => new SkippedTestNodeStateProperty(msg),
        msg => new PassedTestNodeStateProperty(msg),
        msg => new FailedTestNodeStateProperty(msg),
        msg => new TimeoutTestNodeStateProperty(msg),
        msg => new InProgressTestNodeStateProperty(msg),
        msg => new SkippedTestNodeStateProperty(msg)
    ];

    private static void AddResultState(TestNode node, TestResult result, TestExecutionSummary? summary = null)
    {
        if ((int)result >= BasicTestResultStates.Length)
            result = TestResult.Inconclusive;

        if (summary != null && (!string.IsNullOrEmpty(summary.ExceptionFullString) || !string.IsNullOrEmpty(summary.ExceptionType)))
        {
            Func<string, TestNodeStateProperty> factory = TestResultStateFactories[(int)result];
            StringBuilder msg = new StringBuilder();
            msg.AppendLine(BasicTestResultStates[(int)result].Explanation);

            if (!string.IsNullOrEmpty(summary.ExceptionFullString))
            {
                msg.Append(summary.ExceptionFullString);
            }
            else
            {
                msg.Append(summary.ExceptionType!);
                if (!string.IsNullOrEmpty(summary.ExceptionMessage))
                    msg.AppendLine().Append(summary.ExceptionMessage);
                
                if (!string.IsNullOrEmpty(summary.StackTrace))
                    msg.AppendLine().Append(summary.StackTrace);
            }


            node.Properties.Add(factory(msg.ToString()));
        }
        else
        {
            node.Properties.Add(BasicTestResultStates[(int)result]);
        }
    }

    private readonly UnturnedTestExtension _uTest;
    private readonly ITestFrameworkCapabilities _capabilities;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<UnturnedTestFramework> _logger;
    private readonly uTest.Logging.ILogger _uTestLogger;
    private readonly GracefulStopCapability? _stopCapability;
    private readonly ILoggerFactory _loggerFactory;

    // uTest.preferSdk              : bool                      = false
    // uTest.sdkPath                : string                    = ""
    // uTest.serverId               : string                    = "uTest"
    // uTest.maxTestVariations      : ulong                     = 65535
    // uTest.minLogLevel            : LogLevel                  = Information
    // uTest.steamIdGenerationStyle : SteamIdGenerationStyle    = Random
    // remember to add new properties to the schema file
    private readonly IConfiguration _configuration;

    // countdown pattern from https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Extensions.VSTestBridge/SynchronizedSingleSessionVSTestAndTestAnywhereAdapter.cs
    private CountdownEvent? _countdown;

    // null = no session,
    // null UIDs are replaced with string.Empty
    private string? _currentSessionUid;
    private readonly ulong _maxVariations;
    private readonly LogLevel _minLogLevel;
    private readonly SteamIdGenerationStyle _steamIdGenerationStyle;

    private UnturnedLauncher? _serverLauncher;
    private UnturnedLauncher? _clientLauncher;

    private bool _isSessionClosing;

    private readonly Func<RunTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _runTestsAsync;
    private readonly Func<DiscoverTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _discoverTestsAsync;

    /// <inheritdoc />
    public Type[] DataTypesProduced { get; } = [ typeof(TestNodeUpdateMessage) ];

    public UnturnedTestFramework(UnturnedTestExtension uTest, IServiceProvider serviceProvider)
    {
        _uTest = uTest;
        _capabilities = serviceProvider.GetRequiredService<ITestFrameworkCapabilities>();
        _messageBus = serviceProvider.GetMessageBus();
        _configuration = serviceProvider.GetConfiguration();

        _stopCapability = _capabilities.GetCapability<GracefulStopCapability>();
        if (_stopCapability != null)
        {
            _stopCapability.InvokeExecution = StopTestExecutionAsync;
        }

        _loggerFactory = serviceProvider.GetLoggerFactory();
        _logger = _loggerFactory.CreateLogger<UnturnedTestFramework>();
        _uTestLogger = new MTPLogger(_logger);

        _runTestsAsync = RunTestsAsync;
        _discoverTestsAsync = DiscoverTestsAsync;

        if (!ulong.TryParse(_configuration[ConfigurationPrefix + "maxTestVariations"], out _maxVariations))
        {
            _maxVariations = 65535;
        }

        if (!Enum.TryParse(_configuration[ConfigurationPrefix + "minLogLevel"], out _minLogLevel))
        {
            _minLogLevel = LogLevel.Information;
        }

        if (!Enum.TryParse(_configuration[ConfigurationPrefix + "steamIdGenerationStyle"], out _steamIdGenerationStyle))
        {
            _steamIdGenerationStyle = SteamIdGenerationStyle.Random;
        }
    }

    private Task StopTestExecutionAsync(CancellationToken arg)
    {
        return Task.CompletedTask;
    }

    private async Task<List<UnturnedTestInstance>?> GetTests(TestExecutionRequest r, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        ITestRegistrationList? list = _capabilities.GetCapability<ITestRegistrationListCapability>();

        if (list == null)
        {
            _logger.LogInformation("No test registration.");
            return null;
        }

        uTest.Logging.ILogger logger = new MTPLogger(_loggerFactory.CreateLogger(list.GetType().FullName!));
        
        List<UnturnedTestInstance> testInstances = await list.GetMatchingTestsAsync(
            logger,
            MTPFilterHelper.CreateFilter(r.Filter),
            _maxVariations,
            token
        ).ConfigureAwait(false);

        if (testInstances.Count == 0)
        {
            _logger.LogInformation("No tests.");
            return null;
        }

        return testInstances;
    }

    private async Task DiscoverTestsAsync(DiscoverTestExecutionRequest r, ExecuteRequestContext ctx, CancellationToken token = default)
    {
        try
        {
            await _logger.LogInformationAsync($"Discovering tests: {ctx.Request.Session.SessionUid.Value}.");

            List<UnturnedTestInstance>? tests = await GetTests(r, token).ConfigureAwait(false);

            if (tests == null)
                return;

            Task[] publishTasks = new Task[tests.Count];

            SessionUid id = r.Session.SessionUid;

            for (int i = 0; i < tests.Count; ++i)
            {
                UnturnedTestInstance test = tests[i];

                TestNode node = test.CreateTestNode(out TestNodeUid? parentUid);

                node.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);

                publishTasks[i] = ctx.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(id, node, parentUid));
            }

            await Task.WhenAll(publishTasks);
        }
        finally
        {
            ctx.Complete();
        }
    }

    private async Task RunTestsAsync(RunTestExecutionRequest r, ExecuteRequestContext ctx, CancellationToken token = default)
    {
        try
        {
            await _logger.LogInformationAsync($"Discovering tests to run: {ctx.Request.Session.SessionUid.Value}.");

            string? serverId = _configuration["uTest:serverId"]?.Trim();
            if (string.IsNullOrEmpty(serverId))
            {
                serverId = "uTest";
            }
            else if (serverId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                serverId = "uTest";
                await _logger.LogWarningAsync($"Server ID {serverId} has invalid file name characters. Defaulting to 'uTest'.");
            }
            else
            {
                await _logger.LogDebugAsync($"Using server ID: \"{serverId}\".");
            }
            
            List<UnturnedTestInstance>? allTests = await GetTests(r, token).ConfigureAwait(false);
            if (allTests == null)
            {
                return;
            }

            List<List<UnturnedTestInstance>> testGroups = GetTestGroups(allTests);

            string sessionId = r.Session.SessionUid.Value;

            bool serverIsLaunched = false;
            foreach (List<UnturnedTestInstance> tests in testGroups)
            {
                BitArray testReturnMask = new BitArray(tests.Count);

                string commandLine = string.Empty;

                List<Task> runningPublishTasks = new List<Task>();

                JsonSerializer serializer = JsonSerializer.CreateDefault();

                PlayerSimulationMode simulationMode = tests[0].Test.SimulationMode;
                
                UnturnedLauncher launcher;
                bool isClient = simulationMode == PlayerSimulationMode.Singleplayer;

                if (tests[0].Test.DisabledModules is { Length: > 0 } disabledModules)
                {
                    for (int i = 0; i < disabledModules.Length; i++)
                    {
                        string mod = disabledModules[i];
                        commandLine += $"-disableModule/{mod}{(i == disabledModules.Length - 1 ? string.Empty : " ")}";
                    }
                }

                string? sdkPath = null, sdkHomePath = null;
                if (isClient && bool.TryParse(_configuration["uTest:preferSdk"], out bool preferSdk) && preferSdk)
                {
                    sdkPath = _configuration["uTest:sdkPath"];
                    if (string.IsNullOrWhiteSpace(sdkPath))
                    {
                        await _logger.LogWarningAsync("SDK path not configured. Set 'uTest.sdkPath' in the testconfig file.");
                        sdkPath = null;
                    }
                    else if (!Directory.Exists(sdkPath))
                    {
                        await _logger.LogWarningAsync($"SDK Path \"{sdkPath}\" does not exist or isn't a directory.");
                        sdkPath = null;
                    }
                    else
                    {
                        sdkHomePath = Path.Combine(sdkPath, "Builds", "Shared");
                    }
                }

                if (isClient)
                {
                    _clientLauncher ??= new UnturnedLauncher(false, _uTestLogger, _configuration, sdkHomePath);
                    launcher = _clientLauncher;
                    if (serverIsLaunched)
                    {
                        if (_serverLauncher!.HadDummies)
                        {
                            // give steam some time to notice that the dummies were shut down
                            await Task.Delay(5000);
                        }

                        _serverLauncher!.Dispose();
                        _serverLauncher = null;
                        serverIsLaunched = false;
                    }
                }
                else
                {
                    _serverLauncher ??= new UnturnedLauncher(true, _uTestLogger, _configuration);
                    launcher = _serverLauncher;
                }

                using IDisposable resultHandler = launcher.Client.AddMessageHandler<ReportTestResultMessage>(result =>
                {
                    if (!string.Equals(result.SessionUid, sessionId, StringComparison.Ordinal))
                        return false;

                    int index = tests.FindIndex(x => string.Equals(x.Uid, result.Uid, StringComparison.Ordinal));
                    if (index < 0)
                    {
                        _logger.LogWarning($"Received unknown method UID: \"{result.Uid}\"");
                        return true;
                    }

                    UnturnedTestInstance test = tests[index];
                    if (result.Result != TestResult.InProgress)
                    {
                        testReturnMask[index] = true;
                    }

                    _logger.LogInformation($"reported {result.Result} result for test {test.Uid}.");

                    TestNode testNode = test.CreateTestNode(out TestNodeUid? parentUid);

                    TestExecutionSummary? summary = null;
                    if (File.Exists(result.SummaryPath))
                    {
                        using JsonTextReader reader = new JsonTextReader(new StreamReader(result.SummaryPath, Encoding.UTF8, true)) { CloseInput = true };
                        summary = serializer.Deserialize<TestExecutionSummary>(reader);
                    }

                    AddResultState(testNode, result.Result, summary);

                    if (summary != null)
                    {
                        test.AddPropertiesFromSummary(summary, testNode.Properties);
                    }

                    lock (runningPublishTasks)
                    {
                        runningPublishTasks.Add(
                            _messageBus.PublishAsync(this, new TestNodeUpdateMessage(new SessionUid(sessionId), testNode, parentUid))
                        );
                    }

                    return true;
                });

                string settingsFile = launcher.GetSettingsFile();
                _logger.LogInformation($"Creating settings file at \"{settingsFile}\".");
                string? dir = Path.GetDirectoryName(settingsFile);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                Assembly? testAssembly = null;

                List<UnturnedTestReference> exportedTests = new List<UnturnedTestReference>(tests.Count);

                foreach (UnturnedTestInstance test in tests)
                {
                    if (testAssembly == null)
                        testAssembly = test.Test.Method.DeclaringType!.Assembly;

                    exportedTests.Add(new UnturnedTestReference { Uid = test.Uid });
                }

                using (JsonTextWriter writer = new JsonTextWriter(new StreamWriter(settingsFile)))
                {
                    writer.CloseOutput = true;
    #if DEBUG
                    writer.Formatting = Formatting.Indented;
                    writer.IndentChar = '\t';
                    writer.Indentation = 1;
    #else
                    writer.Formatting = Formatting.None;
    #endif

                    serializer.Serialize(writer, new UnturnedTestList
                    {
                        SessionUid = r.Session.SessionUid.Value,
                        Tests = exportedTests,
                        TestListTypeName = typeof(GeneratedTestRegistrationList).AssemblyQualifiedName,
                        IsAllTests = r.Filter == null,
                        TreeNodeFilter = (r.Filter as TreeNodeFilter)?.Filter,
                        CollectTrxProperties = TrxSwitch.HasTrx,
                        TestAssembly = testAssembly!.FullName,
                        ServerId = serverId,
                        MaxTestVariations = _maxVariations,
                        MinimumLogLevel = _minLogLevel,
                        SteamIdGenerationStyle = _steamIdGenerationStyle,
                        // null grp + specific grp for first set so has to be from last test
                        Map = tests[^1].Test.Map
                    });
                }

                Process? process = null;
                if (sdkPath != null)
                {
                    await launcher.LaunchSdk(sdkPath, testAssembly, serverId, token);
                }
                else
                {
                    process = await launcher.LaunchUnturned(out bool isAlreadyLaunched, testAssembly, serverId, commandLine, token);

                    if (!isClient)
                    {
                        serverIsLaunched = true;
                    }

                    await _logger.LogInformationAsync("Launched.");

                    if (isAlreadyLaunched)
                    {
                        await _logger.LogInformationAsync("Unturned already launched.");
                        await launcher.Client.SendAsync(new RefreshTestsMessage(), token);
                    }
                }

                await _logger.LogInformationAsync("Running tests.");

                await launcher.Client.SendAsync(new RunTestsMessage(), token);

                // wait for all tests to execute

                if (sdkPath == null && process != null)
                {
                    // non-SDK: wait for process to end
                    using (token.Register(() =>
                    {
                        // when token is cancelled
                        _logger.LogInformation("Kill requested.");
                        if (process != null)
                        {
                            KillProcess(launcher, process);
                        }
                    }))
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            _logger.LogInformation($"Waiting for process {process.Id} to exit.");
                            process.WaitForExit();
                            _logger.LogInformation("Process exited.");
                        }, TaskCreationOptions.LongRunning);
                    }
                }
                else
                {
                    // SDK: wait for disconnect from pipe
                    TaskCompletionSource<int> disconnected = new TaskCompletionSource<int>();
                    Action onDisconnected = () =>
                    {
                        disconnected.TrySetResult(0);
                    };

                    launcher.Client.Disconnected += onDisconnected;
                    try
                    {
                        using (token.Register(() =>
                        {
                            // when token is cancelled
                            _logger.LogInformation("Kill requested.");
                            launcher.Client.SendAsync(new GracefulShutdownMessage(), CancellationToken.None).Wait(1000);
                            launcher.Client.DisconnectGracefully();
                        }))
                        {
                            _logger.LogInformation("Waiting for pipe to disconnect...");
                            await disconnected.Task;
                            _logger.LogInformation("Client disconnected.");
                        }
                    }
                    finally
                    {
                        launcher.Client.Disconnected -= onDisconnected;
                    }
                }

                Task allPublished = Task.WhenAll(runningPublishTasks);

                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None),
                    allPublished
                );

                if (!allPublished.IsCompleted)
                {
                    await _logger.LogInformationAsync("All not published.");
                    for (int i = 0; i < tests.Count; ++i)
                    {
                        if (testReturnMask[i])
                            continue;

                        TestNode testNode = tests[i].CreateTestNode();
                        AddResultState(testNode, TestResult.Skipped);
                        await _logger.LogInformationAsync($"Skipped {testNode.Uid}.");
                        await _messageBus.PublishAsync(this, new TestNodeUpdateMessage(new SessionUid(sessionId), testNode));
                    }
                }
                else
                {
                    await _logger.LogInformationAsync($"{runningPublishTasks.Count} tasks published.");
                }

                if (isClient)
                {
                    _clientLauncher!.ClientRemoveModule();
                }
            }

            if (_serverLauncher != null)
            {
                _serverLauncher.Dispose();
                _serverLauncher = null;
            }
            if (_clientLauncher != null)
            {
                _clientLauncher.Dispose();
                _clientLauncher = null;
            }
        }
        finally
        {
            ctx.Complete();
        }
    }

    /// <summary>
    /// Separate a list of discovered tests into batches, where each batch will launch a separate instance of U3DS or Unturned.
    /// </summary>
    private static List<List<UnturnedTestInstance>> GetTestGroups(List<UnturnedTestInstance> allTests)
    {
        List<List<UnturnedTestInstance>> groups = new List<List<UnturnedTestInstance>>(4);

        foreach (UnturnedTestInstance instance in allTests)
        {
            bool added = false;
            for (int i = 0; i < groups.Count; ++i)
            {
                UnturnedTestInstance representative = groups[i][0];
                int cmp = CompareWithBatch(instance, representative);
                if (cmp == 0)
                {
                    groups[i].Add(instance);
                    added = true;
                    break;
                }

                if (cmp >= 0)
                    continue;

                groups.Insert(i, new List<UnturnedTestInstance>(allTests.Count / 4) { instance });
                added = true;
                break;
            }

            if (!added)
            {
                groups.Add(new List<UnturnedTestInstance>(allTests.Count / 4) { instance });
            }
        }

        return groups;
    }

    /// <summary>
    /// This function is responsible for separating tests into batches.
    /// Each batch is a separate launch of either U3DS or Unturned. For example, U3DS requires a restart to change the map.
    /// </summary>
    private static int CompareWithBatch(UnturnedTestInstance a, UnturnedTestInstance b)
    {
        UnturnedTest testA = a.Test,
                     testB = b.Test;

        bool isServer;

        if (testA.SimulationMode == PlayerSimulationMode.Singleplayer)
        {
            if (testB.SimulationMode != PlayerSimulationMode.Singleplayer)
                return -1;

            isServer = false;
        }
        else if (testB.SimulationMode == PlayerSimulationMode.Singleplayer)
        {
            return 1;
        }
        else
        {
            isServer = true;
        }

        int cmp;

        if (isServer && testA.Map != null && testB.Map != null)
        {
            cmp = string.Compare(testA.Map, testB.Map, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;
        }

        string[]? disabledModulesA = testA.DisabledModules,
                  disabledModulesB = testB.DisabledModules;

        if (disabledModulesA == null || disabledModulesA.Length == 0)
        {
            if (disabledModulesB != null && disabledModulesB.Length != 0)
                return 1;
        }
        else if (disabledModulesB == null || disabledModulesB.Length == 0)
        {
            return -1;
        }
        else
        {
            cmp = disabledModulesA.Length.CompareTo(disabledModulesB!.Length);
            if (cmp != 0)
                return cmp;

            for (int i = 0; i < disabledModulesA.Length; ++i)
            {
                cmp = string.Compare(disabledModulesA[i], disabledModulesB[i], StringComparison.Ordinal);
                if (cmp != 0)
                    return cmp;
            }
        }

        return 0;
    }

    private static void KillProcess(UnturnedLauncher launcher, Process process)
    {
        launcher.Client.SendAsync(new GracefulShutdownMessage(), CancellationToken.None).Wait(1000);
        try
        {
            process.WaitForExit(1500);
        }
        catch { /* ignored */ }
        try
        {
            process.Kill();
        }
        catch { /* ignored */ }
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(Core(context));

        // non-async implementation
        CreateTestSessionResult Core(CreateTestSessionContext context)
        {
            string uid = context.SessionUid.Value ?? string.Empty;

            string? oldUid = Interlocked.CompareExchange(ref _currentSessionUid, uid, null);
            if (oldUid != null || _isSessionClosing)
            {
                Interlocked.CompareExchange(ref _currentSessionUid, null, uid);
                // session already opened
                return new CreateTestSessionResult
                {
                    ErrorMessage = string.Format(Properties.Resources.LogErrorAlreadyStarted, oldUid),
                    IsSuccess = false
                };
            }

            Interlocked.Exchange(ref _countdown, new CountdownEvent(1))?.Dispose();

            return new CreateTestSessionResult { IsSuccess = true };
        }
    }

    public async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        string uid = context.SessionUid.Value ?? string.Empty;

        _isSessionClosing = true;
        try
        {
            // yes this is a reference comparison, but the reference stays the same througout the execution lifetime so this is fine
            // https://github.com/microsoft/testfx/blob/b6e4331e0c11a01178d4a832cb0eb6beeebe945a/src/Platform/Microsoft.Testing.Platform/Requests/TestHostTestFrameworkInvoker.cs#L52
            string? oldUid = Interlocked.CompareExchange(ref _currentSessionUid, null, uid);
            if (oldUid == null)
            {
                return new CloseTestSessionResult
                {
                    ErrorMessage = string.Format(Properties.Resources.LogErrorNotStarted, uid)
                };
            }

            CountdownEvent? cde = _countdown;
            if (cde != null)
            {
                cde.Signal();

                await cde.WaitAsync(context.CancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _isSessionClosing = false;
        }

        return new CloseTestSessionResult { IsSuccess = true };
    }

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (!ReferenceEquals(context.Request.Session.SessionUid.Value ?? string.Empty, _currentSessionUid))
        {
            return Task.FromException(new NotSupportedException("Invalid session UID."));
        }

        switch (context.Request)
        {
            // supported request types:
            case RunTestExecutionRequest runTestExecutionRequest:
                return RunRequestAsync(runTestExecutionRequest, context, _runTestsAsync);

            case DiscoverTestExecutionRequest discoverTestExecutionRequest:
                return RunRequestAsync(discoverTestExecutionRequest, context, _discoverTestsAsync);
        }

        context.Complete();
        return Task.FromException(new NotSupportedException($"Request {context.Request.GetType().FullName} not supported."));
    }

    // invokes a request with a synchronization gate
    private async Task RunRequestAsync<TRequest>(TRequest r, ExecuteRequestContext ctx, Func<TRequest, ExecuteRequestContext, CancellationToken, Task> operation)
        where TRequest : TestExecutionRequest
    {
        CancellationToken token = ctx.CancellationToken;

        token.ThrowIfCancellationRequested();

        CountdownEvent? cde = _countdown;
        cde?.AddCount();
        try
        {
            await operation(r, ctx, token).ConfigureAwait(false);
        }
        finally
        {
            if (cde == _countdown)
                cde?.Signal();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_stopCapability != null && _stopCapability.InvokeExecution == StopTestExecutionAsync)
        {
            _stopCapability.InvokeExecution = null;
        }

        Interlocked.Exchange(ref _countdown, null)?.Dispose();
    }

    string IExtension.Uid => _uTest.Uid;
    string IExtension.Version => _uTest.Version;
    string IExtension.DisplayName => _uTest.DisplayName;
    string IExtension.Description => _uTest.Description;
    Task<bool> IExtension.IsEnabledAsync() => _uTest.IsEnabledAsync();
}