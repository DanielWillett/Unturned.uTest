using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using uTest.Module;
using uTest.Protocol;

namespace uTest.Runner.Unturned;

internal class UnturnedLauncher : IDisposable
{
    private readonly bool _u3ds;
    private readonly ILogger _logger;
    private readonly object _sync = new object();

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

    private void DisableModule(string installDir)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        if (Directory.Exists(moduleRoot))
        {
            TryWriteModuleDirectoryOrSetEnabled(installDir, null, enabled: false, disableOnly: true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TryWriteModuleDirectoryOrSetEnabled(string installDir, List<Assembly>? testAssemblies, bool enabled = false, bool disableOnly = false)
    {
        string moduleRoot = Path.Combine(installDir, "Modules", "uTest");

        Directory.CreateDirectory(moduleRoot);
        string moduleFile = Path.Combine(moduleRoot, "uTest.module");

        string? json = null;

        Assembly uTestAssembly = typeof(Assert).Assembly;

        string uTestDllLocation;
        try
        {
            uTestDllLocation = uTestAssembly.Location;
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("Unable to locate the Unturned.uTest DLL.", ex);
        }
        string thisDllLocation;
        try
        {
            thisDllLocation = Assembly.GetExecutingAssembly().Location;
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("Unable to locate the Unturned.uTest.Runner DLL.", ex);
        }

        List<string>? assemblyLocations = null;
        if (!disableOnly && testAssemblies != null)
        {
            assemblyLocations = new List<string>(testAssemblies.Count);
            foreach (Assembly testAsm in testAssemblies)
            {
                try
                {
                    string location = testAsm.Location;
                    if (!string.IsNullOrEmpty(location))
                        assemblyLocations.Add(location);
                }
                catch
                {
                    _logger.Warning($"Unable to find location of assembly {testAsm.FullName}.");
                }
            }
        }

        if (string.IsNullOrEmpty(uTestDllLocation) || !File.Exists(uTestDllLocation))
        {
            throw new NotSupportedException("Unable to locate the Unturned.uTest DLL.");
        }
        if (string.IsNullOrEmpty(thisDllLocation) || !File.Exists(thisDllLocation))
        {
            throw new NotSupportedException("Unable to locate the Unturned.uTest.Runner DLL.");
        }

        using (Stream? stream = uTestAssembly.GetManifestResourceStream("uTest.Module.ModuleConfig.json"))
        {
            if (stream != null)
            {
                json = new StreamReader(stream).ReadToEnd();
            }
        }

        if (json == null)
        {
            throw new InvalidProgramException("Unable to find embedded resource for ModuleConfig.json.");
        }

        if (!disableOnly)
        {
            string netstandardDllFile = Path.Combine(moduleRoot, "netstandard.dll");
            // is .NETStandard 2.X target
            if (uTestAssembly
                .GetReferencedAssemblies()
                .Any(x => x.FullName.StartsWith("netstandard, Version=2.")))
            {
                CopyDll("netstandard.dll", moduleRoot, uTestAssembly);
            }
            else if (File.Exists(netstandardDllFile))
            {
                File.Delete(netstandardDllFile);
            }

            CopyDll("System.Runtime.CompilerServices.Unsafe.dll", moduleRoot, uTestAssembly);
            CopyDll("System.Runtime.dll", moduleRoot, uTestAssembly);
        }

        json = json
            .Replace("\"$enabled$\"", enabled ? "true" : "false")
            .Replace("$version$", uTestAssembly.GetName().Version.ToString(4));

        lock (_sync)
        {
            // write module file and copy this DLL to the module root.
            try
            {
                File.WriteAllText(moduleFile, json);
                if (disableOnly)
                    return;

                File.Copy(uTestDllLocation, Path.Combine(moduleRoot, "Unturned.uTest.dll"), true);
                File.Copy(thisDllLocation, Path.Combine(moduleRoot, "Unturned.uTest.Runner.dll"), true);
                if (assemblyLocations != null)
                {
                    for (int i = 0; i < assemblyLocations.Count; i++)
                    {
                        string location = assemblyLocations[i];
                        assemblyLocations[i] = Path.GetFileName(location);
                        File.Copy(location, Path.Combine(moduleRoot, assemblyLocations[i]), true);
                    }
                }

                foreach (string file in Directory.EnumerateFiles(moduleRoot, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    if (string.Equals(fileName, "Unturned.uTest.dll", StringComparison.Ordinal)
                        || string.Equals(fileName, "Unturned.uTest.Runner.dll", StringComparison.Ordinal)
                        || string.Equals(fileName, "System.Runtime.CompilerServices.Unsafe.dll", StringComparison.Ordinal)
                        || string.Equals(fileName, "System.Runtime.dll", StringComparison.Ordinal)
                        || string.Equals(fileName, "netstandard.dll", StringComparison.Ordinal)
                        || assemblyLocations != null && assemblyLocations.Contains(fileName))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(file);
                    }
                    catch (SystemException ex)
                    {
                        _logger.Exception("Failed to delete extra test assembly.", ex);
                    }
                }
            }
            catch (SystemException ex)
            {
                throw new Exception($"Unable to create uTest module in Unturned installation at \"{moduleRoot}\".", ex);
            }
        }

        return;

        static void CopyDll(string name, string moduleRoot, Assembly uTestAssembly)
        {
            string dllFile = Path.Combine(moduleRoot, name);
            using Stream? stream = uTestAssembly.GetManifestResourceStream("uTest.Module." + name);
            if (stream != null)
            {
                using FileStream fs = new FileStream(dllFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                stream.CopyTo(fs);
            }
        }
    }

    public Task<Process> LaunchUnturned(out bool alreadyLaunched, List<Assembly> testAssemblies, CancellationToken token)
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

        string installDir = _unturnedInstallDir.InstallDirectory;

        alreadyLaunched = false;
        return Core(installDir, token);

        async Task<Process> Core(string installDir, CancellationToken token)
        {
            string exe = Path.Combine(installDir, GetExecutableRelativePath());

            string settingsFile = GetSettingsFile();

            string launchArgs = $"-batchmode -nogui -uTestSettings \"{settingsFile}\" +lanserver/uTest";

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
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                try
                {
                    startInfo.UseShellExecute = true;
                }
                catch (NotSupportedException) { }

                token.ThrowIfCancellationRequested();

                disabledModule = false;
                TryWriteModuleDirectoryOrSetEnabled(installDir, testAssemblies, enabled: true);

                if (_task != startupTcs)
                {
                    return await _task.Task;
                }

                process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Unturned.");
                }

                _logger.Info($"Unturned process started with PID {process.Id}.");

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
                        _logger.Info($"Unturned process exited with PID {_processId}, error code: {exitCode}.");
                        ExitCode = (UnturnedTestExitCode)exitCode;
                    }
                    catch
                    {
                        exitCompletionSource.TrySetResult(int.MaxValue);
                        _logger.Info($"Unturned process exited with PID {_processId}.");
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
                        _logger.Info($"Unturned process disconnected with PID {_processId}, error code: {exitCode}.");
                        ExitCode = (UnturnedTestExitCode)exitCode;
                    }
                    catch
                    {
                        exitCompletionSource.TrySetResult(int.MaxValue);
                        _logger.Info($"Unturned process disconnected with PID {_processId}.");
                        ExitCode = (UnturnedTestExitCode)int.MaxValue;
                    }
                };

                Client.Disconnected += onDisconnection;
                try
                {
                    _logger.Info("Initial connection established.");

                    disabledModule = true;
                    DisableModule(installDir);

                    completionSource = new TaskCompletionSource<int>();

                    Action<ITransportMessage> onMessage = message =>
                    {
                        _logger.Info($"Message received: {message.GetType().FullName}.");

                        if (message is LevelLoadedMessage)
                            completionSource.SetResult(0);
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

                _logger.Info("Level loaded.");

                _task?.SetResult(process);
                _task = null;

                return process;
            }
            catch (Exception ex)
            {
                if (!disabledModule)
                    DisableModule(installDir);
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

    private string GetExecutableRelativePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Unturned.exe";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Unturned.app/Contents/MacOS/Unturned";
        }
        
        return _u3ds ? "Unturned_Headless.x86_64" : "Unturned.x86_64";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
    }
}