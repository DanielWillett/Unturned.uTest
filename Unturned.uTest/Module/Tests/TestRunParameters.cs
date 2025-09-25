using System;
using uTest.Discovery;

namespace uTest.Module;

internal sealed class TestRunParameters
{
    internal readonly UnturnedTestInstance Test;
    internal readonly CancellationToken Token;
    internal readonly ILogger Logger;

    internal readonly Action<TestRunParameters, TestRunStopwatchStage>? SignalStart;
    internal readonly Action<TestRunParameters, TestRunStopwatchStage>? SignalEnd;

    internal TestAsyncStateMachine StateMachine;
    internal UnturnedTestList Configuration;

    // created by compiled test
    internal TestContext? Context;

    internal TestRunParameters(
        in UnturnedTestInstance test,
        CancellationToken token,
        ILogger logger,
        TestAsyncStateMachine stateMachine,
        UnturnedTestList configuration,
        Action<TestRunParameters, TestRunStopwatchStage>? signalStart = null,
        Action<TestRunParameters, TestRunStopwatchStage>? signalEnd = null)
    {
        Test = test;
        Token = token;
        Logger = logger;
        StateMachine = stateMachine;
        Configuration = configuration;
        SignalStart = signalStart;
        SignalEnd = signalEnd;
    }
}

internal enum TestRunStopwatchStage
{
    // add translations when adding one in TestAsyncStateMachine
    Setup,
    Execute,
    TearDown
}