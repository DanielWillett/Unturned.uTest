using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace uTest.Module;

internal class TestExecutionPipeline : IExceptionFormatter
{
    private readonly TestRunner _runner;
    private readonly ILogger _logger;
    private readonly UnturnedTestList _testList;
    private readonly MainModule _module;
    private readonly CancellationToken _token;

    private readonly Stopwatch _stopwatch;

    internal UnturnedTestInstanceData? CurrentTest;

    public IExceptionFormatter ExceptionFormatter { get; set; }

    public TestExecutionPipeline(TestRunner runner, ILogger logger, UnturnedTestList testList, MainModule module, IExceptionFormatter? exceptionFormatter, CancellationToken token)
    {
        _runner = runner;
        _logger = logger;
        _testList = testList;
        _module = module;
        _token = token;
        _stopwatch = new Stopwatch();
        ExceptionFormatter = exceptionFormatter ?? this;
    }

    public void InitializeCurrentTest(UnturnedTestInstanceData test)
    {
        CurrentTest = test;
    }

    public async Task<TestExecutionResult> ExecuteTestAsync()
    {
        await GameThread.Switch(_token);

        if (CurrentTest == null)
            throw new InvalidOperationException("Test not loaded.");

        TestExecutionSummary summary;
        try
        {
            RuntimeHelpers.RunClassConstructor(CurrentTest.Instance.Type.TypeHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Test \"{CurrentTest!.Instance.Uid}\" failed with exception when invoking the type initializer for the declaring type.", ex);

            summary = new TestExecutionSummary
            {
                SessionUid = _testList.SessionUid,
                Uid = CurrentTest.Uid
            };

            summary.IncludeException(_testList, ExceptionFormatter, ex);

            return new TestExecutionResult(TestResult.Fail, summary);
        }

        Exception? testException;
        TestContext? context;

        if (CurrentTest.Dummies > 0)
        {
            await _module.Dummies.InitializeDummiesForTestAsync(CurrentTest, _module.CancellationToken);
        }

        _logger.LogInformation("Running test...");
        Task<TestInitErrorCode> task = TestAsyncStateMachine.TryRunTestAsync(CurrentTest, _token, _logger, _stopwatch, _testList, _module, out TestAsyncStateMachine machine);
        try
        {
            if (task is { IsCompleted: true, IsFaulted: false, Result: not TestInitErrorCode.Success })
            {
                return ReportTestInitError(task.Result);
            }

            testException = null;

            TestInitErrorCode errCode;
            try
            {
                errCode = await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                testException = ex;

                // if the test throws an exception its still initialized successfully
                errCode = TestInitErrorCode.Success;
            }

            if (errCode != TestInitErrorCode.Success)
            {
                return ReportTestInitError(errCode);
            }

            // Context can be null if the ITestRunnerActivator failed or some exception was thrown
            context = (TestContext?)machine.Context;

            TestTimingStep? invokeTimingStep = machine.TimingSteps.Find(x => x.Stage == TestRunStopwatchStage.Execute);

            summary = new TestExecutionSummary
            {
                SessionUid = _testList.SessionUid,
                Uid = CurrentTest.Instance.Uid,
                Artifacts = context?.Artifacts,
                TimingSteps = machine.TimingSteps,
                OutputMessages = context?.Messages
            };

            if (invokeTimingStep != null)
            {
                summary.Duration = invokeTimingStep.Duration;
                summary.StartTime = invokeTimingStep.StartTime;
                summary.EndTime = invokeTimingStep.EndTime;
            }
        }
        finally
        {
            await machine.CleanupTestAsync();

            await GameThread.Switch();
            if (machine is { Context: IDisposable disposable })
                disposable.Dispose();
        }

        if (testException != null)
        {
            summary.IncludeException(_testList, ExceptionFormatter, testException);
        }

        summary.StandardOutput = context?.StandardOutput?.ToString();
        if (string.IsNullOrEmpty(summary.StandardOutput)) summary.StandardOutput = null;

        summary.StandardError = context?.StandardError?.ToString();
        if (string.IsNullOrEmpty(summary.StandardError)) summary.StandardError = null;

        if (testException != null)
            _logger.LogError($"Test \"{CurrentTest!.Instance.Uid}\" failed with exception.", testException);

        return new TestExecutionResult(machine.Result ?? TestResult.Fail, summary);
    }

    private TestExecutionResult ReportTestInitError(TestInitErrorCode errCode)
    {
        string err = string.Format(Properties.Resources.TestRunnerError, errCode.ToString());
        return new TestExecutionResult(TestResult.Skipped, new TestExecutionSummary
        {
            SessionUid = _testList.SessionUid,
            Uid = CurrentTest!.Instance.Uid,
            OutputMessages = [ new TestOutputMessage((int)LogSeverity.Critical, err) ],
            ExceptionMessage = err,
            ExceptionFullString = err,
            ExceptionType = nameof(TestInitErrorCode)
        });
    }

    string IExceptionFormatter.FormatException(Exception ex) => ex.ToString();
}
