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

        try
        {
            RuntimeHelpers.RunClassConstructor(CurrentTest.Instance.Type.TypeHandle);
        }
        catch (Exception ex)
        {
            return HandleTestError("Type Initializer", ex);
        }

        Exception? testException;
        TestExecutionSummary summary;
        TestContext context;

        if (CurrentTest.Dummies > 0)
        {
            await _module.Dummies.InitializeDummiesForTestAsync(CurrentTest);
        }

        Task? task = TestAsyncStateMachine.TryRunTestAsync(CurrentTest, _token, _logger, _stopwatch, _testList, _module, out TestAsyncStateMachine machine);
        try
        {

            if (task == null)
            {
                return new TestExecutionResult(TestResult.Skipped, null);
            }

            testException = null;

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                testException = ex;
            }

            context = (TestContext)machine.Context;

            TestTimingStep? invokeTimingStep = machine.TimingSteps.Find(x => x.Stage == TestRunStopwatchStage.Execute);

            summary = new TestExecutionSummary
            {
                SessionUid = _testList.SessionUid,
                Uid = CurrentTest.Instance.Uid,
                Artifacts = context.Artifacts,
                TimingSteps = machine.TimingSteps,
                OutputMessages = context.Messages
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
            await GameThread.Switch();
            if (machine.Context is IDisposable disposable)
                disposable.Dispose();
        }

        if (testException != null)
        {
            if (context.Configuration.CollectTrxProperties)
            {
                summary.StackTrace = testException.StackTrace;
                summary.ExceptionMessage = testException.Message;
                summary.ExceptionType = testException.GetType().FullName;
            }
            summary.ExceptionFullString = ExceptionFormatter.FormatException(testException);
        }

        summary.StandardOutput = context.StandardOutput?.ToString();
        if (string.IsNullOrEmpty(summary.StandardOutput)) summary.StandardOutput = null;

        summary.StandardError = context.StandardError?.ToString();
        if (string.IsNullOrEmpty(summary.StandardError)) summary.StandardError = null;

        if (testException == null)
            return new TestExecutionResult(TestResult.Pass, summary);

        return ReportTestException(testException, summary);

    }

    private TestExecutionResult ReportTestException(Exception ex, TestExecutionSummary summary)
    {
        _logger.LogError($"Test \"{CurrentTest!.Instance.Uid}\" failed with exception.", ex);

        if (ex is ITestResultException testResultException)
        {
            return new TestExecutionResult(testResultException.Result, summary);
        }
        else
        {
            return new TestExecutionResult(TestResult.Fail, summary);
        }
    }

    private TestExecutionResult HandleTestError(string context, Exception ex)
    {
        _logger.LogError($"Error running test \"{CurrentTest!.Instance.Uid}\" ({context}).", ex);
        // todo
        return new TestExecutionResult(TestResult.Fail, null);
    }

    string IExceptionFormatter.FormatException(Exception ex) => ex.ToString();
}
