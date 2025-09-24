using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Discovery;
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

        Type listType = Type.GetType(testList.TestListTypeName, throwOnError: true, ignoreCase: false)!;

        TestExecutionPipeline pipeline = new TestExecutionPipeline(this, _logger, token);

        ITestRegistrationList list = (ITestRegistrationList)Activator.CreateInstance(listType, _module.TestAssembly);

        ITestFilter? filter = null;
        if (!testList.IsAllTests)
        {
            string[] uids = new string[testList.Tests.Count];
            for (int i = 0; i < testList.Tests.Count; ++i)
                uids[i] = testList.Tests[i].Uid;

            filter = new UidListFilter(uids);
        }

        _logger.LogInformation($"Discovering tests in \"{_module.TestAssembly.GetName().FullName}\" ...");

        List<UnturnedTestInstance> tests = await list.GetMatchingTestsAsync(_logger, filter, token).ConfigureAwait(false);

        _logger.LogInformation($"Found {tests.Count} test(s).");

        foreach (UnturnedTestInstance test in tests)
        {
            int testIndex = testList.Tests.FindIndex(x => string.Equals(x.Uid, test.Uid, StringComparison.Ordinal));
            if (testIndex >= 0)
                testList.Tests.RemoveAt(testIndex);

            if (test.Test.Expandable)
            {
                _logger.LogInformation($"Skipping test \"{test.Uid}\"...");
                await ReportTestResult(testList, test.Uid, TestResult.Fail);
                allPass = false;
                continue;
            }

            await ReportTestResult(testList, test.Uid, TestResult.InProgress);

            await GameThread.Switch(token);

            _logger.LogInformation("================================");
            _logger.LogInformation($"Running test \"{test.Uid}\"...");
            _logger.LogInformation(string.Empty);

            pipeline.InitializeCurrentTest(test);

            TestExecutionResult execution = await pipeline.ExecuteTestAsync().ConfigureAwait(false);

            await ReportTestResult(testList, test.Uid, execution);
            await GameThread.Switch(token);

            _logger.LogInformation(string.Empty);
            _logger.LogInformation($"Test result: {execution.Result}");
            _logger.LogInformation("================================");
            _logger.LogInformation(string.Empty);
        }

        if (testList.Tests.Count > 0)
        {
            foreach (UnturnedTestReference testRef in testList.Tests)
            {
                await ReportTestResult(testList, testRef.Uid, TestResult.Skipped);
            }

            return UnturnedTestExitCode.TestsFailed;
        }

        return allPass ? UnturnedTestExitCode.Success : UnturnedTestExitCode.TestsFailed;
    }

    public ConfiguredTaskAwaitable ReportTestResult(UnturnedTestList testList, string uid, TestExecutionResult result)
    {
        ReportTestResultMessage msg = new ReportTestResultMessage
        {
            SessionUid = testList.SessionUid,
            LogPath = result.ExecutionInfoFile ?? string.Empty,
            Result = result.Result,
            Uid = uid
        };

        return _module.Environment.SendAsync(msg).ConfigureAwait(false);
    }
}