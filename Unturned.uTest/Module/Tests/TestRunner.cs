using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
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

        bool allPass = true;

        UnturnedTestList testList = _module.TestList!;
        TestExecutionPipeline pipeline = new TestExecutionPipeline(this, _logger, testList, _module, _module.ExceptionFormatter, token);

        List<UnturnedTestReference> testUids = testList.Tests.ToList();

        _logger.LogDebug($"Starting test run: {testUids.Count} test variation(s). Discovered {_module.Tests.Length} test(s).");

        foreach (UnturnedTestInstanceData test in _module.Tests)
        {
            try
            {
                int testIndex = testUids.FindIndex(x => string.Equals(x.Uid, test.Instance.Uid, StringComparison.Ordinal));
                if (testIndex >= 0)
                    testUids.RemoveAt(testIndex);

                await ReportTestResult(testList, test.Instance.Uid, TestResult.InProgress);

                await GameThread.Switch(token);

                _logger.LogInformation("================================");
                _logger.LogInformation($"Running test \"{test.Instance.Uid}\"...");
                _logger.LogInformation(string.Empty);

                pipeline.InitializeCurrentTest(test);

                TestExecutionResult execution = await pipeline.ExecuteTestAsync().ConfigureAwait(false);

                await ReportTestResult(testList, test.Instance.Uid, execution);
                await GameThread.Switch(token);

                _logger.LogInformation(string.Empty);
                _logger.LogInformation($"Test result: {execution.Result}");
                _logger.LogInformation("================================");
                _logger.LogInformation(string.Empty);

                if (execution.Result != TestResult.Pass)
                    allPass = false;
            }
            catch (Exception ex)
            {
                await ReportTestResult(testList, test.Instance.Uid, new TestExecutionResult(TestResult.Skipped, new TestExecutionSummary
                {
                    SessionUid = testList.SessionUid,
                    Uid = test.Instance.Uid,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    ExceptionFullString = pipeline.ExceptionFormatter.FormatException(ex),
                    OutputMessages = [ new TestOutputMessage((int)LogLevel.Critical, ex.ToString()) ]
                }));
            }
        }

        if (testUids.Count > 0)
        {
            foreach (UnturnedTestReference testRef in testUids)
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