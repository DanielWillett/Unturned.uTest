using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using uTest.Discovery;
using uTest.Protocol;

namespace uTest.Module;

internal class TestRunner
{
    private readonly MainModule _module;
    private readonly ILogger _logger;
    private readonly string _testResultsFolder;
    private readonly JsonSerializer _serializer;

    public TestRunner(MainModule module)
    {
        _module = module;
        _logger = module.Logger;
        _testResultsFolder = Path.Combine(_module.HomeDirectory, "TestResults");
        _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
#if DEBUG
            Formatting = Formatting.Indented,
#else
            Formatting = Formatting.None,
#endif
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    public async Task<UnturnedTestExitCode> RunTestsAsync(CancellationToken token = default)
    {
        if (Directory.Exists(_testResultsFolder))
        {
            Directory.Delete(_testResultsFolder, true);
        }
        Directory.CreateDirectory(_testResultsFolder);

        UnturnedTestList? testList = _module.TestList;
        if (testList == null || testList.Tests == null || testList.Tests.Count == 0)
            return UnturnedTestExitCode.Success;

        bool allPass = true;

        Type listType = Type.GetType(testList.TestListTypeName, throwOnError: true, ignoreCase: false)!;

        TestExecutionPipeline pipeline = new TestExecutionPipeline(this, _logger, testList, token);

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
        string? filePath = null;
        if (result.Summary != null)
        {
            filePath = Path.Combine(_testResultsFolder, Guid.NewGuid().ToString("N") + ".json");
            using JsonTextWriter writer = new JsonTextWriter(new StreamWriter(filePath, false, Encoding.UTF8)) { CloseOutput = true };

            _serializer.Serialize(writer, result.Summary);
            writer.Flush();
        }

        ReportTestResultMessage msg = new ReportTestResultMessage
        {
            SessionUid = testList.SessionUid,
            SummaryPath = filePath ?? string.Empty,
            Result = result.Result,
            Uid = uid
        };

        return _module.Environment.SendAsync(msg).ConfigureAwait(false);
    }
}