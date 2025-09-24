using Microsoft.Testing.Platform.Capabilities.TestFramework;
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
using uTest.Module;
using uTest.Protocol;
using uTest.Runner.Unturned;
using uTest.Runner.Util;

namespace uTest.Runner;

internal class UnturnedTestFramework : ITestFramework, IDisposable, IDataProducer
{
#pragma warning disable TPEXP
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
#pragma warning restore TPEXP

    private static readonly TestNodeStateProperty[] TestResultStates =
    [
        new SkippedTestNodeStateProperty(uTest.Properties.Resources.TestResultInconclusive),
        new PassedTestNodeStateProperty(uTest.Properties.Resources.TestResultPass),
        new FailedTestNodeStateProperty(uTest.Properties.Resources.TestResultFail),
        new CancelledTestNodeStateProperty(uTest.Properties.Resources.TestResultCancelled),
        new TimeoutTestNodeStateProperty(uTest.Properties.Resources.TestResultTimeout),
        new InProgressTestNodeStateProperty(uTest.Properties.Resources.TestResultInProgress),
        new SkippedTestNodeStateProperty(uTest.Properties.Resources.TestResultSkipped)
    ];

    private static void AddResultState(TestNode node, TestResult result)
    {
        if ((int)result >= TestResultStates.Length)
            result = TestResult.Inconclusive;

        node.Properties.Add(TestResultStates[(int)result]);
    }

    private readonly UnturnedTestExtension _uTest;
    private readonly ITestFrameworkCapabilities _capabilities;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<UnturnedTestFramework> _logger;
    private readonly ILogger _uTestLogger;
    private readonly GracefulStopCapability? _stopCapability;
    private readonly ILoggerFactory _loggerFactory;

    // countdown pattern from https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Extensions.VSTestBridge/SynchronizedSingleSessionVSTestAndTestAnywhereAdapter.cs
    private CountdownEvent? _countdown;

    // null = no session,
    // null UIDs are replaced with string.Empty
    private string? _currentSessionUid;

    private UnturnedLauncher? _launcher;

    private bool _isSessionClosing;

    private readonly Func<RunTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _runTestsAsync;
    private readonly Func<DiscoverTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _discoverTestsAsync;

    /// <inheritdoc />
    public Type[] DataTypesProduced { get; } = [ typeof(TestNodeUpdateMessage) ];

    public UnturnedTestFramework(UnturnedTestExtension uTest, IServiceProvider serviceProvider)
    {
        _uTest = uTest;
        _capabilities = serviceProvider.GetRequiredService<ITestFrameworkCapabilities>();
        _messageBus = serviceProvider.GetRequiredService<IMessageBus>();

        _stopCapability = _capabilities.GetCapability<GracefulStopCapability>();
        if (_stopCapability != null)
        {
            _stopCapability.InvokeExecution = StopTestExecutionAsync;
        }

        _loggerFactory = serviceProvider.GetLoggerFactory();
        _logger = _loggerFactory.CreateLogger<UnturnedTestFramework>();
        _uTestLogger = new TFPLogger(_logger);

        _runTestsAsync = RunTestsAsync;
        _discoverTestsAsync = DiscoverTestsAsync;
    }

    private Task StopTestExecutionAsync(CancellationToken arg)
    {
        return Task.CompletedTask;
    }

    private async Task<List<UnturnedTestInstance>?> GetTests(TestExecutionRequest r, CancellationToken token)
    {
        ITestRegistrationList? list = _capabilities.GetCapability<ITestRegistrationList>();

        if (list == null)
        {
            _logger.LogInformation("No test registration.");
            return null;
        }

        Microsoft.Testing.Platform.Logging.ILogger logger = _loggerFactory.CreateLogger(list.GetType().FullName!);
        List<UnturnedTestInstance> testInstances = await list.GetMatchingTestsAsync(r.Filter, logger, token).ConfigureAwait(false);
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
            await _logger.LogInformationAsync($"Discovering tests: {ctx.Request.Session.SessionUid.Value}.");
            
            Debugger.Launch();

            List<UnturnedTestInstance>? tests = await GetTests(r, token).ConfigureAwait(false);
            if (tests == null)
            {
                return;
            }

            _launcher ??= new UnturnedLauncher(true, _uTestLogger);

            string sessionId = r.Session.SessionUid.Value;

            BitArray testReturnMask = new BitArray(tests.Count);

            List<Task> runningPublishTasks = new List<Task>();

            using IDisposable resultHandler = _launcher.Client.AddMessageHandler<ReportTestResultMessage>(result =>
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

                AddResultState(testNode, result.Result);

                lock (runningPublishTasks)
                {
                    runningPublishTasks.Add(
                        _messageBus.PublishAsync(this, new TestNodeUpdateMessage(new SessionUid(sessionId), testNode, parentUid))
                    );
                }

                return true;
            });

            string settingsFile = _launcher.GetSettingsFile();

            List<Assembly> testAssemblies = new List<Assembly>();

            List<UnturnedTestReference> exportedTests = new List<UnturnedTestReference>(tests.Count);
            foreach (UnturnedTestInstance test in tests)
            {
                Assembly asm = test.Test.Method.DeclaringType!.Assembly;
                if (!testAssemblies.Contains(asm))
                    testAssemblies.Add(asm);

                exportedTests.Add(new UnturnedTestReference
                {
                    MetadataToken = test.Test.Method.MetadataToken,
                    Uid = test.Uid
                });
            }

            using (JsonTextWriter writer = new JsonTextWriter(new StreamWriter(settingsFile)))
            {
                writer.CloseOutput = true;
#if DEBUG
                writer.Formatting = Formatting.Indented;
                writer.IndentChar = ' ';
                writer.Indentation = 4;
#else
                writer.Formatting = Formatting.None;
#endif

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, new UnturnedTestList
                {
                    SessionUid = r.Session.SessionUid.Value,
                    Tests = exportedTests
                });
            }

            Process process = await _launcher.LaunchUnturned(out bool isAlreadyLaunched, testAssemblies, token);

            _logger.LogInformation("Launched.");

            if (isAlreadyLaunched)
            {
                await _logger.LogInformationAsync("Unturned already launched.");
                await _launcher.Client.SendAsync(new RefreshTestsMessage(), token);
            }

            _logger.LogInformation("Running tests.");
            await _launcher.Client.SendAsync(new RunTestsMessage(), token);

            // wait for all tests to execute

            using (token.Register(() =>
            {
                _logger.LogInformation("Kill requested.");
                KillProcess(process);
            }))
            {
                await Task.Factory.StartNew(() =>
                {
                    _logger.LogInformation("Waiting for exit.");
                    process.WaitForExit();
                    _logger.LogInformation("Done.");
                }, TaskCreationOptions.LongRunning);
            }

            Task allPublished = Task.WhenAll(runningPublishTasks);

            await Task.WhenAny(
                Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None),
                allPublished
            );

            if (!allPublished.IsCompleted)
            {
                _logger.LogInformation("All not published.");
                for (int i = 0; i < tests.Count; ++i)
                {
                    if (testReturnMask[i])
                        continue;

                    TestNode testNode = tests[i].CreateTestNode();
                    AddResultState(testNode, TestResult.Skipped);
                    _logger.LogInformation($"Skipped {testNode.Uid}.");
                    await _messageBus.PublishAsync(this, new TestNodeUpdateMessage(new SessionUid(sessionId), testNode));
                }
            }
        }
        finally
        {
            ctx.Complete();
        }
    }

    private void KillProcess(Process process)
    {
        _launcher!.Client.SendAsync(new GracefulShutdownMessage(), CancellationToken.None).Wait(1000);
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