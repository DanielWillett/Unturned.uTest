using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using uTest.Logging;
using uTest.Module;
using uTest.Protocol;

namespace uTest.Runner.Unturned;

internal class UnturnedLauncher : IDisposable
{
    private readonly bool _u3ds;
    private readonly ILogger _logger;

    private readonly InstallDirUtility _unturnedInstallDir;

    private int _processId;
    private Process? _process;

    private TaskCompletionSource<Process>? _task;

    public TestEnvironmentClient Client { get; }

    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public string? UnturnedDirectoryOverride
    {
        get => _unturnedInstallDir.OverrideInstallDirectory;
        set => _unturnedInstallDir.OverrideInstallDirectory = value;
    }

    public UnturnedTestExitCode ExitCode { get; private set; }

    public UnturnedLauncher(bool u3ds, ILogger logger, string? unturnedDirectoryOverride = null)
    {
        if (u3ds && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("U3DS is not available for MacOS.");

        _u3ds = u3ds;
        _logger = logger;

        _unturnedInstallDir = new InstallDirUtility(u3ds, logger);

        if (unturnedDirectoryOverride != null)
        {
            _unturnedInstallDir.OverrideInstallDirectory = unturnedDirectoryOverride;
        }
        
        Client = new TestEnvironmentClient(logger);
    }

    private void DisableModule(string installDir, Assembly testAssembly)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        ModuleFiles.IsServer = _u3ds;
        if (!ModuleFiles.DisableModule(moduleRoot, _logger, testAssembly))
        {
            throw new NotSupportedException("Unable to disable test module.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TryWriteModuleDirectoryOrSetEnabled(string installDir, Assembly? testAssembly)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        ModuleFiles.IsServer = _u3ds;
        if (!ModuleFiles.WriteModuleFiles(moduleRoot, _logger, testAssembly))
        {
            throw new NotSupportedException("Unable to write test module.");
        }
    }

    public Task<Process> LaunchUnturned(out bool alreadyLaunched, Assembly testAssembly, CancellationToken token)
    {
        Process? existingProcess = _process;
        if (existingProcess is { HasExited: true })
        {
            Interlocked.CompareExchange(ref _processId, 0, existingProcess.Id);
            Interlocked.CompareExchange(ref _process, null, existingProcess);
        }

        TaskCompletionSource<Process>? task = _task;
        if (task != null)
        {
            alreadyLaunched = false;
            return task.Task;
        }


        if (_processId != 0)
        {
            Process? process;
            if (existingProcess is { HasExited: false } && existingProcess.Id == _processId)
            {
                process = existingProcess;
            }
            else
            {
                try
                {
                    process = Process.GetProcessById(_processId);
                }
                catch
                {
                    process = null;
                }
            }

            if (process != null)
            {
                alreadyLaunched = true;
                _process = process;
                return Task.FromResult(process);
            }
        }

        alreadyLaunched = false;
        return Core(_unturnedInstallDir, token);

        async Task<Process> Core(InstallDirUtility installDirUtil, CancellationToken token)
        {
            string installDir = installDirUtil.InstallDirectory;
            string exe = Path.Combine(installDir, installDirUtil.GetExecutableRelativePath());

            string settingsFile = GetSettingsFile();

            string launchArgs = $"-batchmode " +
                                $"-nogui " +
                                $"-uTestSettings \"{settingsFile}\" " +
                                $"-NetTransport SystemSockets " +
                                $"-LogAssemblyResolve " +
                                $"-LogBadMessages " +
                                $"+lanserver/uTest";

            TaskCompletionSource<Process> startupTcs = new TaskCompletionSource<Process>();
            _task = startupTcs;

            bool disabledModule = true;

            Process? process = null;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exe, launchArgs)
                {
                    CreateNoWindow = false,
                    WorkingDirectory = installDir,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                try
                {
                    startInfo.UseShellExecute = true;
                }
                catch (NotSupportedException) { }

                token.ThrowIfCancellationRequested();

                disabledModule = false;
                TryWriteModuleDirectoryOrSetEnabled(installDir, testAssembly);

                if (_task != startupTcs)
                {
                    return await _task.Task;
                }

                process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Unturned.");
                }

                _logger.LogInformation($"Unturned process started with PID {process.Id}.");

                TaskCompletionSource<int> exitCompletionSource = new TaskCompletionSource<int>();

                _process = process;
                _processId = process.Id;

                process.EnableRaisingEvents = true;
                EventHandler onExit = (sender, _) =>
                {
                    Process process = (Process)sender;
                    try
                    {
                        int exitCode = process.ExitCode;
                        exitCompletionSource.TrySetResult(exitCode);
                        _logger.LogInformation($"Unturned process exited with PID {_processId}, error code: {exitCode}.");
                        ExitCode = (UnturnedTestExitCode)exitCode;
                    }
                    catch
                    {
                        exitCompletionSource.TrySetResult(int.MaxValue);
                        _logger.LogInformation($"Unturned process exited with PID {_processId}.");
                        ExitCode = (UnturnedTestExitCode)int.MaxValue;
                    }
                };
                
                process.Exited += onExit;

                TaskCompletionSource<int> completionSource = new TaskCompletionSource<int>();

                Action onConnection = () => { completionSource.SetResult(0); };

                // wait for initial connection
                Client.Connected += onConnection;
                try
                {
                    await Task.WhenAny(Task.Delay(StartupTimeout, token), completionSource.Task, exitCompletionSource.Task);
                    if (!completionSource.Task.IsCompleted)
                    {
                        if (exitCompletionSource.Task.IsCompleted)
                        {
                            throw new UnturnedStartException($"Exit code: {exitCompletionSource.Task.Result}.");
                        }

                        throw new TimeoutException($"Timed out starting server ({StartupTimeout}).");
                    }
                }
                finally
                {
                    Client.Connected -= onConnection;
                }

                process.Exited -= onExit;
                process.EnableRaisingEvents = false;

                Action onDisconnection = () =>
                {
                    try
                    {
                        int exitCode = _process.ExitCode;
                        exitCompletionSource.TrySetResult(exitCode);
                        _logger.LogInformation($"Unturned process disconnected with PID {_processId}, error code: {exitCode}.");
                        ExitCode = (UnturnedTestExitCode)exitCode;
                    }
                    catch
                    {
                        exitCompletionSource.TrySetResult(int.MaxValue);
                        _logger.LogInformation($"Unturned process disconnected with PID {_processId}.");
                        ExitCode = (UnturnedTestExitCode)int.MaxValue;
                    }
                };

                Client.Disconnected += onDisconnection;
                try
                {
                    _logger.LogInformation("Initial connection established.");

                    completionSource = new TaskCompletionSource<int>();

                    Action<ITransportMessage> onMessage = message =>
                    {
                        _logger.LogInformation($"Message received: {message.GetType().FullName}.");

                        switch (message)
                        {
                            case LevelLoadedMessage:
                                completionSource.SetResult(0);
                                break;

                            case AllInstancesStartedMessage:
                                disabledModule = true;
                                DisableModule(installDir, testAssembly);
                                break;
                        }
                    };


                    Client.MessageReceived += onMessage;
                    try
                    {
                        await Task.WhenAny(Task.Delay(LoadTimeout, token), completionSource.Task, exitCompletionSource.Task);
                        if (!completionSource.Task.IsCompleted)
                        {
                            if (exitCompletionSource.Task.IsCompleted)
                            {
                                throw new UnturnedStartException($"Exit code: {exitCompletionSource.Task.Result}.");
                            }

                            throw new TimeoutException($"Timed out loading level ({LoadTimeout}).");
                        }
                    }
                    finally
                    {
                        Client.MessageReceived -= onMessage;
                    }
                }
                finally
                {
                    Client.Disconnected -= onDisconnection;
                }

                if (exitCompletionSource.Task.IsCompleted)
                {
                    throw new UnturnedStartException($"Exit code: {exitCompletionSource.Task.Result}.");
                }

                _logger.LogInformation("Level loaded.");

                _task?.SetResult(process);
                _task = null;

                return process;
            }
            catch (Exception ex)
            {
                if (!disabledModule)
                    DisableModule(installDir, testAssembly);
                try
                {
                    process?.Kill();
                }
                catch { /* ignored */ }
                process?.Dispose();
                _task?.SetException(ex);
                _task = null;
                throw;
            }
        }
    }

    public string GetSettingsFile()
    {
        return Path.Combine(_unturnedInstallDir.InstallDirectory, "Modules", "uTest", "test-settings.json");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
    }
}