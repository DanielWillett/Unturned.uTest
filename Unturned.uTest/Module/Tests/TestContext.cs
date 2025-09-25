using System;
using System.IO;
using System.Reflection;
using System.Text;
using uTest.Environment;

namespace uTest.Module;

internal class TestContext : ITestContext, IDisposable, ICommandInputOutput
{
    private readonly TestRunParameters _parameters;

    internal List<TestArtifact>? Artifacts;
    internal bool HasStarted = false;
    internal StringBuilder? StandardOutput;
    internal StringBuilder? StandardError;
    internal List<TestOutputMessage>? Messages;
    private readonly TextWriter _prevStdOut, _prevStdErr;

    public UnturnedTestList Configuration => _parameters.Configuration;

    public ITestClass Runner { get; }

    public Type TestClass => _parameters.Test.Type;

    public MethodInfo TestMethod => _parameters.Test.Method;

    public string TestId => _parameters.Test.Uid;

    public CancellationToken CancellationToken => _parameters.Token;

    public ILogger Logger => CommandWindowLogger.Instance;

    internal TestContext(TestRunParameters parameters, ITestClass runner)
    {
        _parameters = parameters;
        Runner = runner;

        CommandWindowSynchronizationHelper.FlushCommandWindow();
        Dedicator.commandWindow.addIOHandler(this);

        TextWriter stdOut = Console.Out;
        TextWriter stdErr = Console.Error;
        _prevStdOut = stdOut;
        _prevStdErr = stdErr;
        Console.SetOut(new StringWriter(StandardOutput = new StringBuilder()));
        Console.SetError(new StringWriter(StandardError = new StringBuilder()));
    }

    public void outputInformation(string information)
    {
        AddMessage((int)LogLevel.Information, information);
    }

    public void outputWarning(string warning)
    {
        AddMessage((int)LogLevel.Warning, warning);
    }

    public void outputError(string error)
    {
        AddMessage((int)LogLevel.Error, error);
    }

    private void AddMessage(int severity, string message)
    {
        if (!_parameters.Configuration.CollectTrxProperties)
            return;

        if (Messages == null)
        {
            Interlocked.CompareExchange(ref Messages, new List<TestOutputMessage>(4), null);
        }

        lock (Messages)
        {
            Messages.Add(new TestOutputMessage(severity, message));
        }
    }

    /// <inheritdoc />
    public void Configure(Action<ITestConfigurationBuilder> configure)
    {
        if (HasStarted) throw new InvalidOperationException();
        
        ITestConfigurationBuilder builder = new TestConfigurationBuilder(this);
        configure(builder);
    }

    /// <inheritdoc />
    public void AddArtifact(string filePath, string? displayName = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            description = null;

        if (Artifacts == null)
        {
            Interlocked.CompareExchange(ref Artifacts, new List<TestArtifact>(4), null);
        }

        lock (Artifacts)
        {
            Artifacts.Add(new TestArtifact(Path.GetFullPath(filePath), displayName ?? Path.GetFileName(filePath), description));
        }
    }

    /// <inheritdoc />
    public void SendTerminalInput(string command)
    {
        if (GameThread.IsCurrent)
        {
            inputCommitted?.Invoke(command);
        }
        else
        {
            SendTerminalInputState state;
            state.Command = command;
            state.Context = this;
            GameThread.RunAndWait(state, static state =>
            {
                state.Context.inputCommitted?.Invoke(state.Command);
            }, _parameters.Token);
        }
    }

    private struct SendTerminalInputState
    {
        public TestContext Context;
        public string Command;
    }

    [DoesNotReturn]
    public void Cancel()
    {
        throw new TestResultException(TestResult.Cancelled);
    }

    [DoesNotReturn]
    public void Ignore()
    {
        throw new TestResultException(TestResult.Skipped);
    }

    [DoesNotReturn]
    public void MarkInconclusive()
    {
        throw new TestResultException(TestResult.Inconclusive);
    }

    [DoesNotReturn]
    public void MarkPass()
    {
        throw new TestResultException(TestResult.Pass);
    }

    [DoesNotReturn]
    public void MarkFailure()
    {
        throw new TestResultException(TestResult.Fail);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CommandWindowSynchronizationHelper.FlushCommandWindow();
        Dedicator.commandWindow.removeIOHandler(this);
        Console.SetOut(_prevStdOut);
        Console.SetError(_prevStdErr);
    }

    public event CommandInputHandler? inputCommitted;

    void ICommandInputOutput.initialize(CommandWindow commandWindow) { }
    void ICommandInputOutput.shutdown(CommandWindow commandWindow) { }
    void ICommandInputOutput.update() { }
}