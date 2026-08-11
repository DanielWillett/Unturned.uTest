using JetBrains.Annotations;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using uTest.Compat.Logging;
using uTest.Dummies;
using uTest.Environment;
using uTest.Patches;

namespace uTest.Module;

internal class TestContext : ITestContext, IDisposable, ICommandInputOutput
{
    private readonly TestRunParameters _parameters;

    internal List<TestArtifact>? Artifacts;
    internal bool HasStarted = false;
    internal StringBuilder StandardOutput;
    internal StringBuilder? StandardError;
    internal List<TestOutputMessage>? Messages;

    private readonly Action<string>? _onLogHandler;

    private bool _didHookDedicatedIO;
    private bool _didHookConsole;

    private readonly UnturnedTestUid _uid;
    private TextWriter? _prevStdOut, _prevStdErr;

    public UnturnedTestList Configuration => _parameters.Configuration;

    public ITestClass Runner { get; }

    public IReadOnlyList<IServersideTestPlayer> Players { get; internal set; }

    public Type TestClass => _parameters.Test.Instance.Type;

    public MethodInfo TestMethod => _parameters.Test.Instance.Method;

    public UnturnedTestUid TestId => _uid;

    public CancellationToken CancellationToken => _parameters.Token;

    public ILogger Logger { get; }

    // invoked by TestCompiler's generated code before the test starts
    [UsedImplicitly]
    internal TestContext(TestRunParameters parameters, ITestClass runner)
    {
        parameters.Module.Logger.LogInformation($"Running testContext ctor, parameters: {parameters == null}, runner: {runner == null};");
        Runner = runner ?? throw new ArgumentNullException(nameof(runner), Properties.Resources.ArgumentNullException_RunnerFactoryReturnedNull);
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _uid = new UnturnedTestUid(parameters.Test.Instance.Uid);
        Players = parameters.Dummies ?? Array.Empty<IServersideTestPlayer>();

        MainModule module = _parameters.Module;

        Logger = module.GetOrCreateLogger("uTest: " + parameters.Test.Instance.Test.DisplayName);
        
        CommandWindowSynchronizationHelper.FlushCommandWindow();

        ILoggerIntegration? loggerIntegration = module.LoggerIntegration;
        StandardOutput = new StringBuilder();
        if (Dedicator.isStandaloneDedicatedServer)
        {
            if (loggerIntegration == null || loggerIntegration.ShouldHookDedicatedIO)
            {
                Dedicator.commandWindow.addIOHandler(this);
                _didHookDedicatedIO = true;
            }

            if (loggerIntegration == null || loggerIntegration.ShouldHookConsole)
            {
                TextWriter stdOut = Console.Out;
                TextWriter stdErr = Console.Error;
                _prevStdOut = stdOut;
                _prevStdErr = stdErr;
                Console.SetOut(new StringWriter(StandardOutput));
                Console.SetError(new StringWriter(StandardError = new StringBuilder()));
                _didHookConsole = true;
            }
        }
        else if (loggerIntegration == null || loggerIntegration.ShouldHookDedicatedIO)
        {
            // client-side

            _onLogHandler = HandleClientsideLog;
            UnturnedLogClientHook.OnLog = _onLogHandler;
        }

        loggerIntegration?.BeginHook((logLvl, msg) =>
        {
            AddMessage(logLvl, msg);
            if (_didHookConsole)
                return;

            StandardOutput.AppendLine(msg);
        });
    }

    [UsedImplicitly] // called by TestCompiler output
    internal Task SetupAsync(CancellationToken token)
    {
        if (Runner is not ITestClassSetup setupListener)
        {
            return SetupDummiesAsync(token);
        }

        ValueTask vt = setupListener.SetupAsync(this, token);
        if (vt.IsCompletedSuccessfully)
            return SetupDummiesAsync(token);

        return Core(vt, token);

        async Task Core(ValueTask setupTask, CancellationToken token)
        {
            await setupTask;
            await SetupDummiesAsync(token);
        }
    }

    [UsedImplicitly] // called by TestCompiler output
    internal Task TearDownAsync(CancellationToken token)
    {
        if (Runner is not ITestClassTearDown tearDownListener)
        {
            return Task.CompletedTask;
        }

        return tearDownListener.TearDownAsync(token).AsTask();
    }

    private Task SetupDummiesAsync(CancellationToken token)
    {
        UnturnedTestInstanceData currentTest = _parameters.Test;
        if (currentTest.Dummies <= 0)
            return Task.CompletedTask;

        _parameters.Logger.LogInformation($"Initializing {currentTest.Dummies} {(currentTest.Dummies == 1 ? "dummy" : "dummies")}...");
        return _parameters.Module.Dummies.InitializeDummiesForTestAsync(this, currentTest, token);
    }

    private void HandleClientsideLog(string message)
    {
        AddMessage(LogLevel.Information, message);
        StandardOutput.AppendLine(message);
    }

    void ICommandInputOutput.outputInformation(string information)
    {
        AddMessage(LogLevel.Information, information);
    }

    void ICommandInputOutput.outputWarning(string warning)
    {
        AddMessage(LogLevel.Warning, warning);
    }

    void ICommandInputOutput.outputError(string error)
    {
        AddMessage(LogLevel.Error, error);
    }

    private void AddMessage(LogLevel severity, string message)
    {
        if (!_parameters.Configuration.CollectTrxProperties)
            return;

        if (Messages == null)
        {
            Interlocked.CompareExchange(ref Messages, new List<TestOutputMessage>(4), null);
        }

        lock (Messages)
        {
            Messages.Add(new TestOutputMessage((int)severity, message));
        }
    }

    /// <inheritdoc />
    public ValueTask ConfigureAsync(Action<ITestConfigurationBuilder> configure)
    {
        if (HasStarted) throw new InvalidOperationException();
        
        ITestConfigurationBuilder builder = new TestConfigurationBuilder(this);
        configure(builder);
        return default;
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
            SendTerminalInputIntl(command);
        }
        else
        {
            SendTerminalInputState state;
            state.Command = command;
            state.Context = this;
            GameThread.RunAndWait(state, static state =>
            {
                state.Context.SendTerminalInputIntl(state.Command);
            }, _parameters.Token);
        }
    }

    private void SendTerminalInputIntl(string command)
    {
        if (_didHookDedicatedIO)
        {
            inputCommitted?.Invoke(command);
        }
        else
        {
            Dedicator.commandWindow.onInputCommitted(command);
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

    public ValueTask SpawnAllPlayersAsync(Action<DummyPlayerJoinConfiguration>? configurePlayers = null, CancellationToken token = default)
    {
        return _parameters.Test.AllocatedDummies is not { Length: > 0 }
            ? default
            : new ValueTask(Core(configurePlayers, token));

        async Task Core(Action<DummyPlayerJoinConfiguration>? configurePlayers, CancellationToken token)
        {
            foreach (IServersideTestPlayer player in _parameters.Test.AllocatedDummies)
            {
                if (player.IsOnline)
                    continue;

                await player.SpawnAsync(configurePlayers, ignoreAlreadyConnected: true, token).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DespawnAllPlayersAsync(CancellationToken token = default)
    {
        return _parameters.Test.AllocatedDummies is not { Length: > 0 }
            ? default
            : new ValueTask(Core(token));

        async Task Core(CancellationToken token)
        {
            List<Exception>? exceptions = null;
            foreach (IServersideTestPlayer player in _parameters.Test.AllocatedDummies)
            {
                if (!player.IsOnline)
                    continue;

                try
                {
                    await player.DespawnAsync(ignoreAlreadyDisconnected: true, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    (exceptions ??= new List<Exception>()).Add(ex);
                }
            }

            if (exceptions != null)
                throw new AggregateException(exceptions);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CommandWindowSynchronizationHelper.FlushCommandWindow();

        _parameters.Module.LoggerIntegration?.EndHook();

        if (_didHookDedicatedIO)
        {
            Dedicator.commandWindow.removeIOHandler(this);
            _didHookDedicatedIO = false;
        }
        
        if (_prevStdOut != null)
        {
            Console.SetOut(_prevStdOut);
            _prevStdOut = null;
        }
        if (_prevStdErr != null)
        {
            Console.SetError(_prevStdErr);
            _prevStdErr = null;
        }
        _didHookConsole = false;

        Interlocked.CompareExchange(ref UnturnedLogClientHook.OnLog, null, _onLogHandler);
    }

    public event CommandInputHandler? inputCommitted;

    void ICommandInputOutput.initialize(CommandWindow commandWindow) { }
    void ICommandInputOutput.shutdown(CommandWindow commandWindow) { }
    void ICommandInputOutput.update() { }
}