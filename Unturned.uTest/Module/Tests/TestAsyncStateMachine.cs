using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using uTest.Compat.DependencyInjection;
using uTest.Compat.Lifetime;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace uTest.Module;

/// <summary>
/// Handles awaiting whatever task type the test uses.
/// </summary>
internal class TestAsyncStateMachine
{
    private TaskCompletionSource<int> _tcs;
    private readonly TestRunParameters _parameters;

    private readonly TestInvoker _testInvoker;
    private readonly TestFinalizer? _testFinalizer;

    private readonly Stopwatch _stopwatch;

    private object? _currentAwaiter;
    private DateTimeOffset _startTime;
    private ValueTask _disconnectPlayersTask;
    private IDisposable? _testInstanceCleanup;

    internal ITestContext Context => _parameters.Context!;
    internal ITestLifetimeIntegration[]? InvokedLifetimes;
    internal TestResult? Result;

    public List<TestTimingStep> TimingSteps { get; }

    private static readonly MethodInfo TryRunTestAsyncGenericMethod = typeof(TestAsyncStateMachine)
        .GetMethod(nameof(TryRunTestAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
        ?? throw new MissingMethodException(nameof(TestAsyncStateMachine), nameof(TryRunTestAsyncGeneric));

    internal TestAsyncStateMachine(UnturnedTestInstanceData test, CancellationToken token, uTest.Logging.ILogger logger, Stopwatch sw, UnturnedTestList testList, MainModule module)
    {
        _parameters = new TestRunParameters(
            test,
            token,
            logger,
            this,
            testList,
            module,
            static (p, s) => p.StateMachine.OnStart(s),
            static (p, s) => p.StateMachine.OnEnd(s)
        );

        (TestInvoker? invoker, TestFinalizer? finalizer) = TestCompiler.CompileTestMethods(_parameters, logger);
        if (invoker == null)
        {
            return;
        }

        _testInvoker = invoker;
        _testFinalizer = finalizer;
        _stopwatch = sw;
        TimingSteps = new List<TestTimingStep>();
    }

    private TestInstance<TRunner> CreateRunner<TRunner>()
        where TRunner : ITestClass
    {
        ITestRunnerActivator? activator = _parameters.Module.TestRunnerActivator;
        if (activator != null)
        {
            return activator.CreateTestInstance<TRunner>();
        }

        return new TestInstance<TRunner>((TRunner)Activator.CreateInstance(typeof(TRunner)), null);
    }

    internal static Task<TestInitErrorCode> TryRunTestAsync(
        UnturnedTestInstanceData test,
        CancellationToken token,
        uTest.Logging.ILogger logger,
        Stopwatch sw,
        UnturnedTestList testList,
        MainModule module,
        out TestAsyncStateMachine machine)
    {
        TestAsyncStateMachine m = new TestAsyncStateMachine(test, token, logger, sw, testList, module);

        if (m._testInvoker == null)
        {
            machine = null!;
            return Task.FromResult(TestInitErrorCode.FailedToCompileTestRunner);
        }

        if (module.Dummies != null && test.Dummies > 0)
        {
            m._parameters.Dummies = module.Dummies.AllocateDummiesToTest(test, out bool overflow);
            if (overflow)
            {
                module.Dummies.DeallocateDummies(test);
                module.Logger.LogInformation("Somehow attempted to allocate more dummies then was originally created.");
                machine = null!;
                return Task.FromResult(TestInitErrorCode.FailedToAllocateDummies);
            }

            int ct = m._parameters.Dummies?.Count ?? 0;
            module.Logger.LogTrace($"Allocated {ct} dumm{(ct == 1 ? "y" : "ies")} to test.");
        }

        MethodInfo tryRunTestMethod = TryRunTestAsyncGenericMethod.MakeGenericMethod(test.Instance.Type);

        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
        m._tcs = tcs;

        machine = m;

        return (Task<TestInitErrorCode>)tryRunTestMethod.Invoke(m, Array.Empty<object>());
    }

    private async Task<TestInitErrorCode> TryRunTestAsyncGeneric<TRunner>()
        where TRunner : ITestClass
    {
        TestRunStopwatchStage stage = TestRunStopwatchStage.Setup;
        OnStart(TestRunStopwatchStage.Setup);
        TestInstance<TRunner> runner = CreateRunner<TRunner>();
        try
        {
            _testInstanceCleanup = runner.Cleanup;

            TestContext context = new TestContext(_parameters, runner.Instance);

            _parameters.Context = context;

            ITestLifetimeIntegration[]? lifetimes = _parameters.Module.TestLifetimeIntegrations;
            if (lifetimes != null)
            {
                InvokedLifetimes = lifetimes;
                for (int i = 0; i < lifetimes.Length; ++i)
                {
                    bool shouldContinue = await lifetimes[i]
                        .BeginTestAsync(_parameters.Test, _parameters.Token)
                        .ConfigureAwait(false);

                    if (shouldContinue)
                        continue;

                    Array.Resize(ref InvokedLifetimes, i + 1);
                    OnEnd(TestRunStopwatchStage.Setup);
                    return TestInitErrorCode.LifetimeCancelled;
                }
            }

            await context.SetupAsync(_parameters.Token);

            OnEnd(TestRunStopwatchStage.Setup);
            stage = TestRunStopwatchStage.Execute;

            // 'OnStart(TestRunStopwatchStage.Execute)' is called by
            // the dynamic function just before calling the test method
            GameThread.Run(StartTest);

            await _tcs.Task;
            Result ??= TestResult.Pass;
        }
        catch (Exception ex)
        {
            if (stage == TestRunStopwatchStage.Setup)
                OnEnd(TestRunStopwatchStage.Setup);
            HandleException(ex);
            throw;
        }
        finally
        {
            // lgtm

            OnStart(TestRunStopwatchStage.TearDown);
            try
            {
                await _parameters.Context!.TearDownAsync(_parameters.Token);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                try
                {
                    Interlocked.Exchange(ref _testInstanceCleanup, null)?.Dispose();
                }
                finally
                {
                    try
                    {
                        await InvokeEndTestLifetimes(Result, _parameters.Token);
                    }
                    finally
                    {
                        try
                        {
                            await CleanupTestAsync();
                        }
                        catch (Exception ex)
                        {
                            HandleException(ex);
                        }
                        finally
                        {
                            OnEnd(TestRunStopwatchStage.TearDown);
                        }
                    }
                }
            }
        }

        return TestInitErrorCode.Success;
    }

    private void HandleException(Exception ex)
    {
        if (ex is ITestResultException resultException)
            Result = resultException.Result;
        else
            Result = TestResult.Fail;
    }

    private void StartTest()
    {
        try
        {
            _testInvoker(_parameters, _parameters.Context!, out _currentAwaiter, HandleTestTaskCompleted);
            if (_currentAwaiter == null)
            {
                OnEnd(TestRunStopwatchStage.Execute);
                _tcs.SetResult(0);
            }
        }
        catch (Exception ex)
        {
            OnEnd(TestRunStopwatchStage.Execute);
            _tcs.SetException(ex);
        }
    }

    // this method is what's passed to the OnCompleted function of the test's task
    private void HandleTestTaskCompleted()
    {
        if (_testFinalizer != null && _currentAwaiter != null)
        {
            try
            {
                _testFinalizer(_currentAwaiter);
                OnEnd(TestRunStopwatchStage.Execute);
            }
            catch (Exception ex)
            {
                OnEnd(TestRunStopwatchStage.Execute);
                _tcs.SetException(ex);
                return;
            }
            finally
            {
                _currentAwaiter = null;
            }
        }

        _tcs.SetResult(0);
    }

    private void OnStart(TestRunStopwatchStage stage)
    {
        uTest.TestContext.Current = Context;

        _parameters.Logger.LogTrace($"Started stage: {stage}.");

        _startTime = DateTimeOffset.Now;
        _stopwatch.Restart();
    }

    private void OnEnd(TestRunStopwatchStage stage)
    {
        _parameters.Logger.LogTrace($"Ended stage: {stage}.");

        _stopwatch.Stop();
        DateTimeOffset stopTime = DateTimeOffset.Now;
        TimingSteps.Add(
            new TestTimingStep(stage switch
                {
                    TestRunStopwatchStage.Setup => "Setup",
                    TestRunStopwatchStage.Execute => "Execute",
                    TestRunStopwatchStage.TearDown => "TearDown",
                    _ => stage.ToString()
                }, stage switch
                {
                    TestRunStopwatchStage.Setup => "Test Setup",
                    TestRunStopwatchStage.Execute => "Invoke Test",
                    TestRunStopwatchStage.TearDown => "Test Tear-Down",
                    _ => stage.ToString()
                }, _startTime, stopTime, _stopwatch.Elapsed
            )
            {
                Stage = stage
            }
        );

        if (stage != TestRunStopwatchStage.Execute)
            return;

        _disconnectPlayersTask = Context.DespawnAllPlayersAsync();
        uTest.TestContext.Current = null!;
    }

    internal async Task InvokeEndTestLifetimes(TestResult? result, CancellationToken token = default)
    {
        ITestLifetimeIntegration[]? integrations = Interlocked.Exchange(ref InvokedLifetimes, null);
        if (integrations == null)
            return;

        List<Exception>? exceptions = null;
        ExceptionDispatchInfo? single = null;
        for (int i = integrations.Length - 1; i >= 0; --i)
        {
            try
            {
                await integrations[i]
                    .EndTestAsync(_parameters.Test, result, token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HandleException(ex);
                single ??= ExceptionDispatchInfo.Capture(ex);
                (exceptions ??= new List<Exception>()).Add(ex);
            }
        }

        if (exceptions != null)
        {
            if (exceptions.Count == 1 && single != null)
                single.Throw();
            else
                throw new AggregateException("Multiple ITestLifetimeIntegrations threw an exception from EndTestAsync.", exceptions);
        }
    }

    // can safely be called multiple times.
    internal async Task CleanupTestAsync()
    {
        try
        {
            ValueTask task = _disconnectPlayersTask;
            _disconnectPlayersTask = default;

            await task.ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await InvokeEndTestLifetimes(Result, CancellationToken.None);
            }
            finally
            {
                _parameters.Module.Dummies?.DeallocateDummies(_parameters.Test);
            }
        }
    }

    public void Dispose()
    {
        if (Context is IDisposable disp)
            disp.Dispose();

        Interlocked.Exchange(ref _testInstanceCleanup, null)?.Dispose();
    }
}

internal enum TestInitErrorCode
{
    Success,
    FailedToAllocateDummies,
    FailedToCompileTestRunner,
    LifetimeCancelled
}