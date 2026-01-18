using System;
using System.Diagnostics;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace uTest.Module;

internal sealed class TestAsyncStateMachine
{
    private TaskCompletionSource<int> _tcs;
    private readonly TestRunParameters _parameters;

    private readonly Action _continuation;
    private readonly CompiledTest _compilationResult;

    private readonly Stopwatch _stopwatch;

    private int _state;
    private object? _currentAwaiter;
    private DateTimeOffset _startTime;
    private ValueTask _disconnectPlayersTask;

    internal ITestContext Context => _parameters.Context!;

    public List<TestTimingStep> TimingSteps { get; }

    internal static Task? TryRunTestAsync(UnturnedTestInstanceData test, CancellationToken token, ILogger logger, Stopwatch sw, UnturnedTestList testList, MainModule module, out TestAsyncStateMachine machine)
    {
        TestAsyncStateMachine m = new TestAsyncStateMachine(test, token, logger, sw, testList, module);

        if (module.Dummies != null && test.Dummies > 0)
        {
            m._parameters.Dummies = module.Dummies.AllocateDummiesToTest(test, out bool overflow);
            if (overflow)
            {
                module.Dummies.DeallocateDummies(test);
                module.Logger.LogInformation("Somehow attempted to allocate more dummies then was originally created.");
                machine = null!;
                return null;
            }
        }

        if (m._compilationResult == null)
        {
            machine = null!;
            return null;
        }

        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
        m._tcs = tcs;
        m.Continue();

        machine = m;
        return Core(tcs, m);

        static async Task Core(TaskCompletionSource<int> tcs, TestAsyncStateMachine m)
        {
            try
            {
                await tcs.Task;
            }
            finally
            {
                await m.CleanupTestAsync();
            }
        }
    }

    private async Task CleanupTestAsync()
    {
        await _disconnectPlayersTask.ConfigureAwait(false);
        _parameters.Module.Dummies?.DeallocateDummies(_parameters.Test);
    }

    public TestAsyncStateMachine(UnturnedTestInstanceData test, CancellationToken token, ILogger logger, Stopwatch sw, UnturnedTestList testList, MainModule module)
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

        CompiledTest? compilationResult = TestCompiler.CompileTest(_parameters, logger);
        if (compilationResult == null)
        {
            return;
        }

        _continuation = Continue;
        _compilationResult = compilationResult;
        _stopwatch = sw;
        TimingSteps = new List<TestTimingStep>();
    }

    private Exception? _pendingException;

    private void OnStart(TestRunStopwatchStage stage)
    {
        uTest.TestContext.Current = Context;

        _startTime = DateTimeOffset.Now;
        _stopwatch.Restart();
    }

    private void OnEnd(TestRunStopwatchStage stage)
    {
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

        _disconnectPlayersTask = Context.DespawnAllPlayersAsync();
        uTest.TestContext.Current = null!;
    }

    private void Continue()
    {
        try
        {
            if (_compilationResult(_parameters, ref _state, ref _currentAwaiter, _continuation))
                return;

            if (_pendingException != null)
                _tcs.SetException(_pendingException);
            else
                _tcs.SetResult(0);
        }
        catch (Exception ex)
        {
            if (_state < TestCompiler.StateTearDown)
            {
                _state = TestCompiler.StateRerunTearDown;
                _pendingException = ex;
                try
                {
                    if (_compilationResult(_parameters, ref _state, ref _currentAwaiter, _continuation))
                    {
                        return;
                    }
                }
                catch { /* ignored */ }
            }

            _tcs.SetException(_pendingException ?? ex);
            _pendingException = null;
        }
    }
}
