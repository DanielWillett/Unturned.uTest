using System;
using System.Reflection;
using uTest.Protocol;

namespace uTest.Module;

internal class TestRunner
{
    private readonly MainModule _module;
    private readonly ILogger _logger;

    public TestRunner(MainModule module)
    {
        _module = module;
        _logger = module.Logger;
    }

    public async Task<UnturnedTestExitCode> RunTestsAsync(CancellationToken token = default)
    {
        UnturnedTestList? testList = _module.TestList;
        if (testList == null || testList.Tests == null || testList.Tests.Count == 0)
            return UnturnedTestExitCode.Success;

        bool allPass = true;

        TestExecutionPipeline pipeline = new TestExecutionPipeline(this, _logger, token);

        foreach (UnturnedTestReference test in testList.Tests)
        {
            Type type = Type.GetType(test.TypeName, throwOnError: true, ignoreCase: false)!;

            MethodBase method;
            try
            {
                method = type.Module.ResolveMethod(test.MetadataToken);
            }
            catch
            {
                await ReportTestResult(testList, test, TestResult.Skipped).ConfigureAwait(false);
                continue;
            }

            if (method is not MethodInfo methodInfo)
            {
                await ReportTestResult(testList, test, TestResult.Skipped).ConfigureAwait(false);
                continue;
            }

            await ReportTestResult(testList, test, TestResult.InProgress).ConfigureAwait(false);

            await GameThread.Switch(token);

            _logger.Info("================================");
            _logger.Info($"Running test \"{test.Uid}\"...");
            _logger.Info(string.Empty);

            pipeline.InitializeCurrentTest(test, methodInfo);

            TestExecutionResult execution = await pipeline.ExecuteTestAsync().ConfigureAwait(false);

            await ReportTestResult(testList, test, execution).ConfigureAwait(false);
            await GameThread.Switch(token);

            _logger.Info(string.Empty);
            _logger.Info($"Test result: {execution.Result}");
            _logger.Info("================================");
            _logger.Info(string.Empty);
        }

        return allPass ? UnturnedTestExitCode.Success : UnturnedTestExitCode.TestsFailed;
    }

    public async Task ReportTestResult(UnturnedTestList testList, UnturnedTestReference testRef, TestExecutionResult result)
    {
        ReportTestResultMessage msg = new ReportTestResultMessage
        {
            SessionUid = testList.SessionUid,
            LogPath = result.ExecutionInfoFile ?? string.Empty,
            Result = result.Result,
            Uid = testRef.Uid
        };

        await _module.Environment.SendAsync(msg);
    }
}