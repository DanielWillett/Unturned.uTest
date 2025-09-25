using Newtonsoft.Json;
using SDG.Framework.IO;
using SDG.Framework.Modules;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using uTest.Protocol;
using Component = UnityEngine.Component;

namespace uTest.Module;

/// <summary>
/// Main class for Unturned functionality of uTest.
/// </summary>
internal class MainModule : MonoBehaviour, IDisposable
{
    private const string TestFileCommandLine = "-uTestSettings";

    private bool _hasQuit;

    private bool _nextFrameLevelIsLoaded;
    private bool _hasReceivedRunTests;
    private float _sentLevelLoadedRealtime;

    private readonly CommandLineString _clTestFile = new CommandLineString(TestFileCommandLine);

    /// <summary>
    /// If startup ran into an error.
    /// </summary>
    public bool IsFaulted { get; private set; }

    /// <summary>
    /// List of all tests to be ran.
    /// </summary>
    public UnturnedTestList? TestList { get; private set; }

    /// <summary>
    /// Server used to communicate with the test runner.
    /// </summary>
    public TestEnvironmentServer Environment { get; private set; } = null!;

    /// <summary>
    /// The Unturned logger.
    /// </summary>
    public ILogger Logger => CommandWindowLogger.Instance;

    /// <summary>
    /// The assembly that contains the running tests.
    /// </summary>
    public Assembly TestAssembly { get; private set; } = null!;
    
    /// <summary>
    /// The directory this module is stored in.
    /// </summary>
    public string HomeDirectory { get; private set; } = null!;

    /// <summary>
    /// Entrypoint for module.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Initialize()
    {
        GameThread.Setup();

        bool failedToParse = false;
        if (!_clTestFile.hasValue || !TryRefreshTestFile(out failedToParse))
        {
            CommandWindow.LogError(
                !failedToParse
                    ? $"""Test file not provided or unable to be read. Unturned must be launched with command line: {TestFileCommandLine} "File\Path\To\Tests.yml"."""
                    : "Test file failed to parse valid JSON."
            );
            IsFaulted = true;
            return;
        }

        Version version = Assembly.GetExecutingAssembly().GetName().Version;

        SDG.Framework.Modules.Module? module = ModuleHook.getModuleByName("uTest");
        if (module == null)
        {
            CommandWindow.LogError("Failed to find uTest module.");
            IsFaulted = true;
            return;
        }

        Regex assemblies = new Regex(
            @"(?:Unturned\.uTest, Version=[\d\.]+, Culture=neutral, PublicKeyToken=null)|(netstandard, Version=2\.1\.0\.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51)",
            RegexOptions.Singleline
        );

        HomeDirectory = Path.GetFullPath(module.config.DirectoryPath);

        TestAssembly = Array.Find(module.assemblies, x => !assemblies.IsMatch(x.FullName));
        if (TestAssembly == null)
        {
            CommandWindow.LogError("Failed to find test assembly.");
            IsFaulted = true;
            return;
        }

        Dedicator.commandWindow.title = Properties.Resources.WindowTitle;

        // todo: docs
        string log = $"""
                      Launching uTest v{version} on Unturned v{Provider.APP_VERSION} by DanielWillett (@danielwillett on Discord).
                      - GitHub           : https://github.com/DanielWillett/Unturned.uTest
                      - Docs             : 
                      - Report a problem : https://github.com/DanielWillett/Unturned.uTest/issues
                      = uTest is licensed under GPL-3.0
                      |  Unturned.uTest  Copyright (C) {DateTime.Now.Year}  Daniel Willett
                      |  This program comes with ABSOLUTELY NO WARRANTY.
                      |  This is free software, and you are welcome to redistribute it
                      |  under certain conditions.
                      = View full license at https://github.com/DanielWillett/Unturned.uTest/blob/master/LICENSE.txt.
                      """;

        CommandWindow.Log(log);

        Environment = new TestEnvironmentServer(Logger);
        Environment.Disconnected += () =>
        {
            if (_hasQuit)
                return;

            Logger.LogWarning("Lost contact with runner, shutting down...");
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Shutdown due to losing contact with runner.", UnturnedTestExitCode.GracefulShutdown);
            });
        };

        Level.onPostLevelLoaded += OnPostLevelLoaded;

        Environment.AddMessageHandler<RefreshTestsMessage>(_ =>
        {
            TryRefreshTestFile(out bool _);
            return true;
        });

        Environment.AddMessageHandler<RunTestsMessage>(_ =>
        {
            _hasReceivedRunTests = true;
            TestRunner runner = new TestRunner(this);

            Task.Run(async () =>
            {
                UnturnedTestExitCode exitCode;
                try
                {
                    exitCode = await runner.RunTestsAsync();
                }
                catch (Exception ex)
                {
                    await Logger.LogErrorAsync("Error running tests.", ex);
                    exitCode = UnturnedTestExitCode.StartupFailure;
                }

                GameThread.Run(
                    exitCode,
                    exitCode => ForceQuitGame("Test run completed.", exitCode)
                );
            });

            return true;
        });

        Environment.AddMessageHandler<GracefulShutdownMessage>(_ =>
        {
            GameThread.Run(this, me =>
            {
                me.ForceQuitGame("Graceful shutdown from uTest.", UnturnedTestExitCode.GracefulShutdown);
            });
            return true;
        });
    }

    private void OnPostLevelLoaded(int level)
    {
        Level.onPostLevelLoaded -= OnPostLevelLoaded;
        _nextFrameLevelIsLoaded = true;
    }

    /// <summary>
    /// Ran one frame after level loaded event so <see cref="Level.isLoaded"/> is <see langword="true"/>.
    /// </summary>
    private void OnLevelLoaded()
    {
        Task.Run(async () =>
        {
            await Environment.SendAsync(new LevelLoadedMessage());
            await Logger.LogInformationAsync("Sent level loaded");
        });
        _sentLevelLoadedRealtime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (_nextFrameLevelIsLoaded)
        {
            _nextFrameLevelIsLoaded = false;
            OnLevelLoaded();
        }

        if (!_hasReceivedRunTests && _sentLevelLoadedRealtime - Time.realtimeSinceStartup > 2)
        {
            ForceQuitGame("Timed out waiting for RunTests message from runner.", UnturnedTestExitCode.StartupFailure);
        }

        GameThread.RunContinuations();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_hasQuit)
            return;

        GameThread.FlushRunAndWaits();

        _hasQuit = true;
        IsFaulted = false;
        Environment?.Dispose();
        Environment = null!;
    }

    public bool TryRefreshTestFile(out bool failedToParse)
    {
        failedToParse = false;

        string path = _clTestFile.value;
        try
        {
            UnturnedTestList? list = IOUtility.jsonDeserializer.deserialize<UnturnedTestList>(path);
            if (list == null)
            {
                failedToParse = true;
                return false;
            }

            list.Tests ??= new List<UnturnedTestReference>(0);

            CommandWindow.Log($"Test session: \"{list.SessionUid}\". {list.Tests.Count} test(s).");

            TestList = list;
        }
        catch (JsonException ex)
        {
            // bad JSON
            failedToParse = true;
            CommandWindow.LogError(ex);
            return false;
        }
        catch (Exception ex)
        {
            // file not found, etc
            CommandWindow.LogError(ex);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Quits the game instantly with an exit code.
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    [DoesNotReturn]
#endif
    public void ForceQuitGame(string reason, UnturnedTestExitCode exitCode)
    {
        GameThread.Assert();

        FieldInfo? wasQuitGameCalled = typeof(Provider).GetField("wasQuitGameCalled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (wasQuitGameCalled != null)
        {
            wasQuitGameCalled.SetValue(null, true);
        }
        else
        {
            CommandWindow.LogWarning("uTest failed to find field 'Provider.wasQuitGameCalled'.");
        }

        UnturnedLog.info($"uTest Quit game: {reason}. Exit code: {(int)exitCode} ({exitCode}).");
        Dispose();
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}

/// <summary>
/// The entrypoint for uTest when loaded as an Unturned module.
/// </summary>
internal class MainModuleLoader : IModuleNexus
{
    private object? _module;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.initialize()
    {
        try
        {
            Init();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Init()
    {
        GameObject go = new GameObject("uTest");
        Object.DontDestroyOnLoad(go);
        
        MainModule module = go.AddComponent<MainModule>();
        _module = module;

        try
        {
            module.Initialize();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            if (!module.IsFaulted)
                throw;
        }
        finally
        {
            if (module.IsFaulted)
            {
                module.ForceQuitGame("uTest initialization failed. See log.", UnturnedTestExitCode.StartupFailure);
            }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.shutdown()
    {
        if (_module is IDisposable disposable)
            disposable.Dispose();

        if (_module is Component comp)
            Object.Destroy(comp.gameObject);
    }
}