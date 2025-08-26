using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace uTest.Module;

internal class TestExecutionPipeline
{
    private readonly TestRunner _runner;
    private readonly ILogger _logger;
    private readonly CancellationToken _token;

    public UnturnedTestReference? CurrentTest { get; set; }

    public TestExecutionPipeline(TestRunner runner, ILogger logger, CancellationToken token)
    {
        _runner = runner;
        _logger = logger;
        _token = token;
    }

    private UnturnedTestReference? _currentTest;
    private MethodInfo? _testMethodInfo;

    private Type? _testType;

    public void InitializeCurrentTest(UnturnedTestReference test, MethodInfo methodInfo)
    {
        _currentTest = test;
        _testMethodInfo = methodInfo;
        _testType = methodInfo.DeclaringType!;
    }

    public async Task<TestExecutionResult> ExecuteTestAsync()
    {
        await GameThread.Switch(_token);

        if (_currentTest == null || _testMethodInfo == null)
            throw new InvalidOperationException("Test not loaded.");

        try
        {
            RuntimeHelpers.RunClassConstructor(_testType!.TypeHandle);
        }
        catch (Exception ex)
        {
            return HandleTestError("Type Initializer", ex);
        }

        object runner;
        try
        {
            runner = Activator.CreateInstance(_testType);
        }
        catch (Exception ex)
        {
            return HandleTestError("Runner Instance Creation", ex);
        }

        // todo: support better test binding
        Action action;
        try
        {
            action = (Action)_testMethodInfo.CreateDelegate(typeof(Action), runner);
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
        _logger.Exception($"Test \"{_currentTest!.Uid}\" failed with exception.", ex);
        // todo
        return new TestExecutionResult(TestResult.Fail, null);
    }

    private TestExecutionResult HandleTestError(string context, Exception ex)
    {
        _logger.Exception($"Error running test \"{_currentTest!.Uid}\".", ex);
        // todo
        return new TestExecutionResult(TestResult.Fail, null);
    }
}
