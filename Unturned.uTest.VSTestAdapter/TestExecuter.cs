using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Collections.Generic;

namespace uTest.Adapter;

[ExtensionUri(Uri)]
internal class TestExecuter : BaseTestInterface, ITestExecutor2
{
    internal const string Uri = "executor://uTest/UnturnedTestExecutor";

    private int _pid;

    private void TryAttach(IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (_pid == 0 || runContext is not { IsBeingDebugged: true } || frameworkHandle is not IFrameworkHandle2 frameworkHandle2)
            return;

        if (!frameworkHandle2.AttachDebuggerToProcess(_pid))
        {
            Warn(Properties.Resources.LogFailedToDebug);
        }
    }

    /// <inheritdoc />
    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        Init((IMessageLogger?)frameworkHandle ?? ConsoleMessageLogger.Instance);

        List<TestCase> cases = tests != null ? tests as List<TestCase> ?? new List<TestCase>(tests) : new List<TestCase>(0);

        TryAttach(runContext, frameworkHandle);

        Info("Running:");
        foreach (TestCase c in cases)
        {
            Info($" * {c.DisplayName}");
        }
    }

    /// <inheritdoc />
    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        List<string> cases = sources != null ? sources as List<string> ?? new List<string>(sources) : new List<string>(0);

        Info("Running:");
        foreach (string c in cases)
        {
            Info($" * {c}");
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {

    }

    /// <inheritdoc />
    public bool ShouldAttachToTestHost(IEnumerable<string>? sources, IRunContext runContext)
    {
        return false;
    }

    /// <inheritdoc />
    public bool ShouldAttachToTestHost(IEnumerable<TestCase>? tests, IRunContext runContext)
    {
        return false;
    }
}