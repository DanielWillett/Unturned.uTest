using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
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

    private readonly UnturnedTestExtension _uTest;
    private readonly ITestFrameworkCapabilities _capabilities;
    private readonly ILogger<UnturnedTestFramework> _logger;
    private readonly GracefulStopCapability? _stopCapability;

    // countdown pattern from https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Extensions.VSTestBridge/SynchronizedSingleSessionVSTestAndTestAnywhereAdapter.cs
    private CountdownEvent? _countdown;

    // null = no session,
    // null UIDs are replaced with string.Empty
    private string? _currentSessionUid;

    private bool _isSessionClosing;

    private readonly Func<RunTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _runTestsAsync;
    private readonly Func<DiscoverTestExecutionRequest, ExecuteRequestContext, CancellationToken, Task> _discoverTestsAsync;

    /// <inheritdoc />
    public Type[] DataTypesProduced { get; } = [ typeof(TestNodeUpdateMessage) ];

    public UnturnedTestFramework(UnturnedTestExtension uTest, IServiceProvider serviceProvider)
    {
        _uTest = uTest;
        _capabilities = serviceProvider.GetRequiredService<ITestFrameworkCapabilities>();

        _stopCapability = _capabilities.GetCapability<GracefulStopCapability>();
        if (_stopCapability != null)
        {
            _stopCapability.InvokeExecution = StopTestExecutionAsync;
        }

        _logger = serviceProvider.GetLoggerFactory().CreateLogger<UnturnedTestFramework>();

        _runTestsAsync = RunTestsAsync;
        _discoverTestsAsync = DiscoverTestsAsync;
    }

    private Task StopTestExecutionAsync(CancellationToken arg)
    {
        return Task.CompletedTask;
    }

    private async Task DiscoverTestsAsync(DiscoverTestExecutionRequest r, ExecuteRequestContext ctx, CancellationToken token = default)
    {
        try
        {
            await _logger.LogInformationAsync($"Discovering tests: {ctx.Request.Session.SessionUid.Value}.");

            ITestRegistrationList? list = _capabilities.GetCapability<ITestRegistrationList>();

            if (list == null)
            {
                _logger.LogInformation("No test registration.");
                return;
            }

            List<UnturnedTest> tests = await list.GetTestsAsync(token);
            if (tests.Count == 0)
            {
                _logger.LogInformation("No tests.");
                return;
            }

            Task[] publishTasks = new Task[tests.Count];

            SessionUid id = r.Session.SessionUid;

            for (int i = 0; i < tests.Count; ++i)
            {
                UnturnedTest test = tests[i];

                TestNode node = new TestNode
                {
                    DisplayName = test.DisplayName,
                    Uid = new TestNodeUid(test.Uid)
                };

                node.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);

                test.AddProperties(node);

                TestNodeUid? parentUid = test.ParentUid == null ? null : new TestNodeUid(test.ParentUid);

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

        }
        finally
        {
            ctx.Complete();
        }
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