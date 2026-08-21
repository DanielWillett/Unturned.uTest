using Microsoft.Testing.Platform.Configurations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using uTest.Compat;
using uTest.Logging;
using uTest.Module;
using uTest.Protocol;
using uTest.Runner.Util;

namespace uTest.Runner.Unturned;

internal class UnturnedLauncher : IDisposable
{
    // for use in the steam connect link (steam://launch/id/OPTION#)
    private const string UnturnedAppId = "304930";
    private const string UnturnedNoBattlEyeOptionIndex = "1";

    private readonly bool _u3ds;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    private readonly InstallDirUtility _unturnedInstallDir;
    internal bool HadDummies;

    private int _processId;
    private Process? _process;

    private TaskCompletionSource<Process>? _task;

    public TestEnvironmentClient Client { get; }

    // originally did a short amount of time here but OpenMod can start up pretty slowly the first time
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMinutes(1.5);
    public TimeSpan SdkStartupTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan SteamLaunchTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan UnityLaunchTimeout { get; set; } = TimeSpan.FromMinutes(1);

    public string? UnturnedDirectoryOverride
    {
        get => _unturnedInstallDir.OverrideInstallDirectory;
        set => _unturnedInstallDir.OverrideInstallDirectory = value;
    }

    public UnturnedTestExitCode ExitCode { get; private set; }

    public UnturnedLauncher(bool u3ds, ILogger logger, IConfiguration configuration, string? unturnedDirectoryOverride = null)
    {
        if (u3ds && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("U3DS is not available for MacOS.");

        _u3ds = u3ds;
        _logger = logger;
        _configuration = configuration;

        _unturnedInstallDir = new InstallDirUtility(u3ds, logger);

        if (unturnedDirectoryOverride != null)
        {
            _unturnedInstallDir.OverrideInstallDirectory = unturnedDirectoryOverride;
        }
        
        Client = new TestEnvironmentClient(logger);
    }

    private void DisableModule(string installDir, Assembly testAssembly, string serverId)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        ModuleFiles.IsServer = _u3ds;
        ModuleFiles.UpdateOpenModDependency(moduleRoot, _logger, remove: true, serverId);
        if (!ModuleFiles.DisableModule(moduleRoot, _logger, testAssembly))
        {
            throw new NotSupportedException("Unable to disable test module.");
        }
    }

    internal void ClientRemoveModule()
    {
        if (_u3ds)
            return;

        string moduleRoot = Path.Combine(_unturnedInstallDir.InstallDirectory, "Modules", "uTest");

        ModuleFiles.IsServer = false;
        ModuleFiles.ClientRemoveModule(moduleRoot, _logger);
    }

    private void TryWriteModuleDirectoryOrSetEnabled(string installDir, Assembly? testAssembly, string serverId)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        ModuleFiles.IsServer = _u3ds;

        ModuleFiles.UpdateOpenModDependency(moduleRoot, _logger, remove: false, serverId);

        if (ModuleFiles.WriteModuleFiles(moduleRoot, _logger, testAssembly))
            return;

        try
        {
            ModuleFiles.UpdateOpenModDependency(moduleRoot, _logger, remove: true, serverId);
        }
        catch { /* ignored */ }
        throw new NotSupportedException("Unable to write test module. Ensure that the server and all clients closed correctly.");
    }

    private void DisableServersideOnlyChanges(string installDir)
    {
        if (!CompatibilityInformation.IsOpenModInstalled)
        {
            return;
        }

        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        foreach (BootstrapperModuleConfigFile file in ModuleFiles.Files.OfType<BootstrapperModuleConfigFile>())
        {
            CompatibilityInformation.IsOpenModInstalled = false;
            try
            {
                if (!file.TryWrite(moduleRoot, _logger, out _, null))
                {
                    _logger.LogWarning("Failed to disable OpenMod dependency for clients.");
                }
            }
            finally
            {
                CompatibilityInformation.IsOpenModInstalled = true;
            }
        }
    }

    public async Task LaunchSdk(string sdkPath, Assembly testAssembly, string serverId, CancellationToken token)
    {
        if (UnturnedDirectoryOverride == null)
            throw new InvalidOperationException("SDK path not configured.");

        string installDir = Path.Combine(sdkPath, "Builds", "Shared");

        string mainWindowTitle = Path.GetFileName(sdkPath);

        List<Process> existingUnityProcesses = Process
            .GetProcessesByName("Unity Editor")
            .Concat(Process.GetProcessesByName("Unity"))
            .ToList();

        Process? u3SdkEditorProcess = existingUnityProcesses.Find(
            p => p.MainWindowTitle.StartsWith(mainWindowTitle, StringComparison.Ordinal)
        );

        existingUnityProcesses.ForEach(p =>
        {
            if (p != u3SdkEditorProcess)
                p.Dispose();
        });

        if (u3SdkEditorProcess == null)
        {
            _logger.LogInformation($"{mainWindowTitle} is not open, attempting to launch.");
            if (!LaunchUnity(sdkPath, ref u3SdkEditorProcess))
            {
                _logger.LogWarning("uTest failed to launch Unity. This may cause problems.");
            }
        }

        _logger.LogInformation("Waiting for user to start playing in editor...");

        UnturnedBootState state = new UnturnedBootState
        {
            DisabledModule = false,
            MovedModule = false
        };

        TryWriteModuleDirectoryOrSetEnabled(installDir, testAssembly, serverId);    

        try
        {
            await WaitForUnturnedStartup(u3SdkEditorProcess, true, installDir, testAssembly, serverId, state, token);
        }
        catch
        {
            if (!state.DisabledModule)
                DisableModule(installDir, testAssembly, serverId);

            if (!state.MovedModule)
                ClientRemoveModule();

            throw;
        }

        u3SdkEditorProcess?.Dispose();
    }

    private bool LaunchUnity(string sdkPath, ref Process? unityProcess)
    {
        if (!UnityInstallationHelper.TryGetUnityVersionFromProject(sdkPath, out UnityEngineVersion expectedVersion))
        {
            _logger.LogWarning($"Unable to determine Unity version from SDK at \"{sdkPath}\".");
            return false;
        }

        if (!UnityInstallationHelper.TryFindUnityInstall(expectedVersion, out string? exe, out UnityEngineVersion version))
        {
            _logger.LogWarning($"Failed to find a Unity installation >= {version}.");
            return false;
        }

        if (version != expectedVersion)
        {
            _logger.LogWarning($"Expected Unity {expectedVersion} but found {version} at {exe}.");
            if (version.Major != expectedVersion.Major)
                return false;
        }

        string args = $"-projectPath \"{sdkPath}\" ‑ignorecompilererrors";

        _logger.LogDebug($"Starting \"{exe}\" with args: '{args}'");

        ProcessStartInfo startInfo = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            WindowStyle = ProcessWindowStyle.Maximized
        };

        try
        {
            startInfo.UseShellExecute = false;
        }
        catch (NotSupportedException) { }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning($"Failed to start Unity {version} at \"{exe}\".");
                return false;
            }

            unityProcess = process;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning($"Failed to find a Unity installation >= {version} at {exe}.");
            return false;
        }

        _logger.LogDebug($"Waiting for Unity {version} to start...");
        try
        {
            if (!process.WaitForInputIdle((int)UnityLaunchTimeout.TotalMilliseconds))
            {
                _logger.LogWarning($"Timed out waiting for Unity {version} to start.");
            }
            else
            {
                _logger.LogDebug($"Unity {version} started.");
            }
        }
        catch (NotSupportedException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Error waiting on Unity to start. This can maybe be ignored.");
            _logger.LogWarning(ex.ToString());
        }

        return true;
    }

    public Task<Process> LaunchUnturned(out bool alreadyLaunched, Assembly testAssembly, string serverId, string commandLine, CancellationToken token)
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
        return Core(_unturnedInstallDir, serverId, commandLine, token);

        async Task<Process> Core(InstallDirUtility installDirUtil, string serverId, string? commandLine, CancellationToken token)
        {
            string installDir = installDirUtil.InstallDirectory;
            string exe = Path.Combine(installDir, installDirUtil.GetExecutableRelativePath());

            string settingsFile = GetSettingsFile();

            string launchArgs = string.Empty;

            bool foundSteamExe = true;

            bool launchedUsingSteamWebProtocol = false;

            if (_u3ds)
            {
                launchArgs =  "-batchmode " +
                              "-nogui " +
                             $"-uTestSettings \"{settingsFile.Replace("\\", "/")}\" " +
                              "-NetTransport SystemSockets " +
                              "-LogAssemblyResolve " +
                              "-LogBadMessages " +
                             (string.IsNullOrEmpty(commandLine) ? string.Empty : commandLine + " ") +
                             $"+lanserver/{serverId}";
            }
            else
            {
                launchArgs = $"-uTestSettings \"{settingsFile.Replace("\\", "/")}\" " +
                             (string.IsNullOrEmpty(commandLine) ? string.Empty : commandLine + " ") +
                              "-LogAssemblyResolve " +
                              "-LogBadMessages";

                // launch with steam URL:
                // CreateUnturnedLaunchArgsForSteam(ref exe, ref launchArgs, out foundSteamExe, commandLine);
                // launchedUsingSteamWebProtocol = true;
            }

            TaskCompletionSource<Process> startupTcs = new TaskCompletionSource<Process>();
            _task = startupTcs;

            UnturnedBootState state = new UnturnedBootState
            {
                DisabledModule = true,
                MovedModule = true
            };

            Process? process = null;
            try
            {
                if (string.IsNullOrEmpty(launchArgs))
                    await _logger.LogInformationAsync($"Starting Unturned with shell: \"{exe}\".");
                else
                    await _logger.LogInformationAsync($"Starting Unturned at \"{exe}\" with args \"{launchArgs}\".");

                string processName = GetUnturnedClientProcessName();
                if (!_u3ds && launchedUsingSteamWebProtocol)
                {
                    Process[] existingUnturnedProcesses = Process.GetProcessesByName(processName);
                    if (existingUnturnedProcesses.Length > 0)
                    {
                        foreach (Process p in existingUnturnedProcesses) p.Dispose();
                        await _logger.LogErrorAsync($"Unturned is already open. PID(s): {string.Join(", ", existingUnturnedProcesses.Select(x => x.Id))}");
                        throw new InvalidOperationException("Close Unturned before attempting to run singleplayer tests.");
                    }

                    await _logger.LogDebugAsync($"Found no existing processes by the name \"{processName}\".");
                }

                // it opens in the background by default if I don't do this
                if (_u3ds && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const string conhost = @"C:\Windows\System32\conhost.exe";
                    if (File.Exists(conhost))
                    {
                        launchArgs = $"-- \"{exe}\" {launchArgs}";
                        exe = conhost;
                    }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(exe, launchArgs)
                {
                    WorkingDirectory = installDir,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                try
                {
                    startInfo.UseShellExecute = !(_u3ds || foundSteamExe);
                }
                catch (NotSupportedException) { }

                token.ThrowIfCancellationRequested();

                state.DisabledModule = false;
                state.MovedModule = false;
                TryWriteModuleDirectoryOrSetEnabled(installDir, testAssembly, serverId);

                if (_task != startupTcs)
                {
                    return await _task.Task;
                }

                process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Unturned.");
                }

                if (!_u3ds && launchedUsingSteamWebProtocol)
                {
                    DateTime start = DateTime.UtcNow;
                    Process? newestUnturnedProcess = null;
                    do
                    {
                        await Task.Delay(1000, token);
                        Process[] unturnedProcess = Process.GetProcessesByName(processName);
                        if (unturnedProcess.Length == 0)
                            continue;

                        newestUnturnedProcess = unturnedProcess.Aggregate((a, b) =>
                        {
                            DateTime atime, btime;
                            try { atime = a.StartTime; }
                            catch (InvalidOperationException) { atime = DateTime.MinValue; }

                            try { btime = b.StartTime; }
                            catch (InvalidOperationException) { btime = DateTime.MinValue; }

                            return atime > btime ? a : b;
                        });

                        foreach (Process p in unturnedProcess.Where(x => x != newestUnturnedProcess))
                            p.Dispose();

                        break;

                    } while (DateTime.UtcNow - start < SteamLaunchTimeout);

                    if (newestUnturnedProcess == null)
                    {
                        throw new TimeoutException($"Timed out waiting on Steam to launch Unturned ({SteamLaunchTimeout}). Maybe it's downloading an update?");
                    }

                    _logger.LogInformation($"Found Unturned process launched by Steam: {newestUnturnedProcess.Id}. Steam PID: {process.Id}.");
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to kill Steam launcher process.");
                        _logger.LogWarning(ex.ToString());
                    }
                    finally
                    {
                        process.Dispose();
                    }

                    process = newestUnturnedProcess;
                }

                await _logger.LogInformationAsync($"Unturned process started with PID {process.Id}.");

                await WaitForUnturnedStartup(process, false, installDir, testAssembly, serverId, state, token);

                _task?.SetResult(process);
                _task = null;

                return process;
            }
            catch (Exception ex)
            {
                if (!state.DisabledModule)
                    DisableModule(installDir, testAssembly, serverId);

                if (!state.MovedModule)
                    ClientRemoveModule();

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

    private class UnturnedBootState
    {
        public bool DisabledModule;
        public bool MovedModule;
    }

    private async Task WaitForUnturnedStartup(
        Process? process,
        bool isSdk,
        string installDir,
        Assembly testAssembly,
        string serverId,
        UnturnedBootState state,
        CancellationToken token)
    {
        TaskCompletionSource<int> exitCompletionSource = new TaskCompletionSource<int>();

        _process = process;
        _processId = process?.Id ?? 0;

        EventHandler onExit = (sender, _) =>
        {
            Process process = (Process)sender;
            try
            {
                int exitCode = process.ExitCode;
                exitCompletionSource.TrySetResult(exitCode);
                _logger.LogInformation($"{(isSdk ? "Unity" : "Unturned")} process exited with PID {_processId}, error code: {exitCode}.");
                ExitCode = (UnturnedTestExitCode)exitCode;
            }
            catch
            {
                exitCompletionSource.TrySetResult(int.MaxValue);
                _logger.LogInformation($"{(isSdk ? "Unity" : "Unturned")} process exited with PID {_processId}.");
                ExitCode = (UnturnedTestExitCode)int.MaxValue;
            }
        };

        if (process != null)
        {
            process.EnableRaisingEvents = true;
            process.Exited += onExit;
        }

        TaskCompletionSource<int> completionSource = new TaskCompletionSource<int>();

        Action onConnection = () => { completionSource.SetResult(0); };

        // wait for initial connection
        Client.Connected += onConnection;
        try
        {
            TimeSpan timeout = isSdk ? SdkStartupTimeout : StartupTimeout;
            await Task.WhenAny(Task.Delay(timeout, token), completionSource.Task, exitCompletionSource.Task);
            if (!completionSource.Task.IsCompleted)
            {
                if (exitCompletionSource.Task.IsCompleted)
                {
                    throw new UnturnedStartException($"Exit code: {exitCompletionSource.Task.Result}.");
                }

                throw new TimeoutException($"Timed out starting server ({timeout}).");
            }
        }
        finally
        {
            Client.Connected -= onConnection;
        }

        if (process != null)
        {
            process.Exited -= onExit;
            process.EnableRaisingEvents = false;
        }

        Action onDisconnection = () =>
        {
            try
            {
                int exitCode = _process?.ExitCode ?? -1;
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
            await _logger.LogInformationAsync("Initial connection established.");

            completionSource = new TaskCompletionSource<int>();

            Action<ITransportMessage> onMessage = message =>
            {
                _logger.LogInformation($"Message received: {message.GetType().FullName}.");

                switch (message)
                {
                    case LevelLoadedMessage:
                        completionSource.SetResult(0);
                        break;

                    case ReadyToRevertModuleChanges:
                        DisableServersideOnlyChanges(installDir);
                        _ = Client.SendAsync(new ServerModuleChangesReverted(), token);
                        break;

                    case AllInstancesStartedMessage:
                        state.DisabledModule = true;
                        HadDummies = true;
                        DisableModule(installDir, testAssembly, serverId);
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

        await _logger.LogInformationAsync("Level loaded.");
    }

    private static string GetUnturnedClientProcessName()
    {
        return "Unturned";
    }

    private void CreateUnturnedLaunchArgsForSteam(ref string exe, ref string launchArgs, out bool foundSteamExe, string commandLine)
    {
        const string protocolLink = $"steam://launch/{UnturnedAppId}/OPTION{UnturnedNoBattlEyeOptionIndex}";
        string steamDir;
        try
        {
            steamDir = _unturnedInstallDir.SteamDirectory;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogDebug($"Failed to find steam executable.{System.Environment.NewLine}{ex}");
            foundSteamExe = false;
            return;
        }

        exe = protocolLink;
        foundSteamExe = false;
        if (string.IsNullOrEmpty(steamDir))
            return;

        string? fileName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            fileName = "steam.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            fileName = "steam";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            fileName = "steam.sh";
        else
            return;

        string loc = Path.Combine(steamDir, fileName);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !File.Exists(loc))
        {
            loc += ".sh";
        }

        if (File.Exists(loc))
        {
            exe = loc;
            launchArgs = $"-- \"{protocolLink}\"";
            foundSteamExe = true;
        }
        else
        {
            _logger.LogDebug($"Failed to find steam executable, expected at {loc}.");
        }
    }

    public string GetSettingsFile()
    {
        // also update client-side launch (clients can't get launch args)
        return Path.Combine(_unturnedInstallDir.InstallDirectory, "Modules", "uTest", "test-settings.json");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
    }
}