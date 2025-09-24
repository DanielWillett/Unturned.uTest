using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Discovery;

namespace uTest.Module;

internal class TestExecutionPipeline
{
    private readonly TestRunner _runner;
    private readonly ILogger _logger;
    private readonly CancellationToken _token;

    internal UnturnedTestInstance CurrentTest;

    public TestExecutionPipeline(TestRunner runner, ILogger logger, CancellationToken token)
    {
        _runner = runner;
        _logger = logger;
        _token = token;
    }

    public void InitializeCurrentTest(UnturnedTestInstance test)
    {
        CurrentTest = test;
    }

    public async Task<TestExecutionResult> ExecuteTestAsync()
    {
        await GameThread.Switch(_token);

        if (CurrentTest.Method == null)
            throw new InvalidOperationException("Test not loaded.");

        try
        {
            RuntimeHelpers.RunClassConstructor(CurrentTest.Type.TypeHandle);
        }
        catch (Exception ex)
        {
            return HandleTestError("Type Initializer", ex);
        }

        object runner;
        try
        {
            runner = Activator.CreateInstance(CurrentTest.Type);
        }
        catch (Exception ex)
        {
            return HandleTestError("Runner Instance Creation", ex);
        }

        // todo: support better test binding
        Action action;
        try
        {
            action = (Action)CurrentTest.Method.CreateDelegate(typeof(Action), runner);
        }
        catch (Exception ex)
        {
            return HandleTestError("Bind Test Method", ex);
        }

        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ReportTestException(ex);
        }
        finally
        {
            sw.Stop();
        }

        return new TestExecutionResult(TestResult.Pass, null);
    }

    private TestExecutionResult ReportTestException(Exception ex)
    {
        _logger.LogError($"Test \"{CurrentTest.Uid}\" failed with exception.", ex);
        // todo
        return new TestExecutionResult(TestResult.Fail, null);
    }

    private TestExecutionResult HandleTestError(string context, Exception ex)
    {
        _logger.LogError($"Error running test \"{CurrentTest.Uid}\".", ex);
        // todo
        return new TestExecutionResult(TestResult.Fail, null);
    }
}
