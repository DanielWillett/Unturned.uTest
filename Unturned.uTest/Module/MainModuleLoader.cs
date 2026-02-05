using SDG.Framework.Modules;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Compat;
using Component = UnityEngine.Component;

namespace uTest.Module;

/// <summary>
/// The entrypoint for uTest when loaded as an Unturned module.
/// </summary>
internal class MainModuleLoader : IModuleNexus
{
    private object? _module;
    private static readonly CommandLineFlag? IsDummyFlag = new CommandLineFlag(false, "-uTestSteamId");
    internal static string? _homeDir;
    internal static SDG.Framework.Modules.Module? _sdgModule;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.initialize()
    {
        if (IsDummyFlag!.value)
            return;

        GameThread.Setup();
        _sdgModule = ModuleHook.getModuleByName("uTest");
        if (_sdgModule?.config.DirectoryPath == null)
        {
            _homeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        else
        {
            _homeDir = _sdgModule.config.DirectoryPath;
        }

        if (_sdgModule == null || !Directory.Exists(_homeDir))
        {
            ForceQuitGame("uTest initialization failed. Failed to find uTest module.", UnturnedTestExitCode.StartupFailure);
            return;
        }

        // initialize CompatibilityInformation class
        foreach ((string modName, Action<bool> callback) in CompatibilityInformation.CompatibleModules)
        {
            callback(ModuleHook.getModuleByName(modName) is { config.IsEnabled: true });
        }
        CompatibilityInformation.IsUnturnedTestInstalled = true;

        Init();
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
            module.Initialize(_homeDir!, _sdgModule!);
            if (module.IsFaulted)
            {
                module.ForceQuitGame("uTest initialization failed. See log", UnturnedTestExitCode.StartupFailure);
            }
        }
        catch (QuitGameException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError(ex);
            module.ForceQuitGame("Exception thrown during uTest initialization. See log", UnturnedTestExitCode.StartupFailure);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    void IModuleNexus.shutdown()
    {
        CompatibilityInformation.IsUnturnedTestInstalled = false;
        foreach ((_, Action<bool> callback) in CompatibilityInformation.CompatibleModules)
        {
            callback(false);
        }

        if (_module is IDisposable disposable)
            disposable.Dispose();

        if (_module is Component comp)
            Object.Destroy(comp.gameObject);
    }

    private static void ForceQuitGame(string reason, UnturnedTestExitCode exitCode)
    {
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
        Application.Quit((int)exitCode);
        throw new QuitGameException();
    }
}