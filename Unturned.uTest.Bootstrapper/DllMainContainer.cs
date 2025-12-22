using System;
using SDG.Framework.Modules;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace uTest.Bootstrapper;

internal static class DllMainContainer
{
    [ModuleInitializer]
    internal static void DllMain()
    {
        if (Environment.CommandLine.IndexOf("-uTestSteamId", StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        if (!Thread.CurrentThread.IsGameThread())
        {
            // This will always be false if we're not running in Unturned.
            return;
        }

        // Unturned will call this on the game thread, therefore we're running in Unturned
        InitLibs();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitLibs()
    {
        string? homeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        homeDir = string.IsNullOrEmpty(homeDir) ? string.Empty : Path.GetFullPath(homeDir);

        new ModuleLibraryHelper(homeDir).AddFallbackLibraries();
    }
}

public class NullModule : IModuleNexus
{
    public void initialize() { }
    public void shutdown() { }
}