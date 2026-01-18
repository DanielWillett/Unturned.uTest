using SDG.Framework.Modules;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace uTest.Bootstrapper;

internal class ModuleLibraryHelper(string moduleHomePath)
{
    private readonly string _moduleHomePath = moduleHomePath;

    internal void AddFallbackLibraries()
    {
        // note: We add these later so they don't override any given in other modules.
        //       They're EXEs so they're not auto-loaded on startup, not ideal but no other way to do it
        //       if they're auto-loaded they can make it look like other modules are working when they're actually not

        Version _3100 = new Version(3, 1, 0, 0);
        Version _4000 = new Version(4, 0, 0, 0);

        Log("=== BOOTSTRAPPING uTEST ===");
        Log("Adding fallback libraries for uTest.");

        TryAddFallback(_3100, "Microsoft.Extensions.FileSystemGlobbing.exe", out _);

        TryAddFallback(_3100, "Microsoft.Extensions.Logging.Abstractions.exe", out AssemblyName msExtLoggerAbs);
        AssemblyName msExtDepInjAbs;
        if (VersionCompare(msExtLoggerAbs.Version, new Version(8, 0, 0, 0)) >= 0)
        {
            TryAddFallback(msExtLoggerAbs.Version, "Microsoft.Extensions.DependencyInjection.Abstractions.exe", out msExtDepInjAbs);

            if (VersionCompare(msExtLoggerAbs.Version, new Version(9, 0, 0, 0)) >= 0)
            {
                TryAddFallback(msExtLoggerAbs.Version, "System.Diagnostics.DiagnosticSource.exe", out _);
            }
        }
        else
        {
            TryAddFallback(_3100, "Microsoft.Extensions.DependencyInjection.Abstractions.exe", out msExtDepInjAbs);
        }

        if (VersionCompare(msExtDepInjAbs.Version, new Version(6, 0, 0, 0)) >= 0)
        {
            TryAddFallback(msExtDepInjAbs.Version, "Microsoft.Bcl.AsyncInterfaces.exe", out msExtDepInjAbs);
        }

        TryAddFallback(new Version(1, 1, 2, 0), "DanielWillett.SpeedBytes.exe", out _);
        TryAddFallback(_4000, "ReflectionTools.exe", out _, load: true);
        TryAddFallback(new Version(4, 0, 0, 1), "ReflectionTools.Harmony.exe", out _);
        TryAddFallback(new Version(1, 0, 0, 0), "ModularRpcs.exe", out AssemblyName modRpcs, load: true);
        TryAddFallback(modRpcs.Version, "ModularRpcs.NamedPipes.exe", out _, load: true);
        TryAddFallback(modRpcs.Version, "ModularRpcs.Unity.exe", out _, load: true);
        TryAddFallback(new Version(2, 4, 2, 0), "0Harmony.exe", out _);
        TryAddFallback(_4000, "System.Collections.Concurrent.exe", out _);
        TryAddFallback(_4000, "System.Diagnostics.Debug.exe", out _);
        TryAddFallback(_4000, "System.Diagnostics.Tools.exe", out _);
        TryAddFallback(_4000, "System.Globalization.exe", out _);
        TryAddFallback(_4000, "System.Linq.exe", out _);
        TryAddFallback(_4000, "System.ObjectModel.exe", out _);
        TryAddFallback(_4000, "System.Reflection.exe", out _);
        TryAddFallback(_4000, "System.Reflection.Extensions.exe", out _);
        TryAddFallback(_4000, "System.Text.RegularExpressions.exe", out _);
        TryAddFallback(_4000, "System.Threading.Tasks.exe", out _);
        TryAddFallback(new Version(4, 2, 0, 1), "System.Threading.Tasks.Extensions.exe", out _);
        TryAddFallback(new Version(4, 0, 5, 0), "System.Buffers.exe", out _);
        Log("=== BOOTSTRAPPING uTEST ===");
    }

    internal bool TryAddFallback(Version referencedVersion, string fileName, out AssemblyName an, bool load = false)
    {
        string fullPath = Path.Combine(_moduleHomePath, fileName);
        AssemblyName includedAssembly = AssemblyName.GetAssemblyName(fullPath);
        an = includedAssembly;

        foreach (KeyValuePair<AssemblyName, string> x in ModuleHook.discoveredNameToPath.ToList())
        {
            if (!x.Key.Name.Equals(includedAssembly.Name, StringComparison.Ordinal))
                continue;

            if (x.Key.Version != null && x.Key.Version >= referencedVersion)
            {
                Log($"Skipped loading fallback version of {includedAssembly.Name}, already registered as {x.Key} at \"{x.Value}\". Minimum needed is {referencedVersion}.");
                an = x.Key;
                return false;
            }

            Log($"Loading fallback version for {includedAssembly.Name}. Registered version is too old, minimum needed is {referencedVersion}. Consider upgrading the version in your module's Library folder (at \"{x.Value}\").");
            ModuleHook.discoveredNameToPath.Remove(x.Key);
            ModuleHook.discoveredNameToPath[includedAssembly] = fullPath;
            if (load)
            {
                Assembly.LoadFrom(fullPath);
                Log($"  Pre-loaded {includedAssembly.Name}.");
            }
            return true;
        }

        Log($"Loading fallback version for {includedAssembly.Name}. There is no registered version, minimum needed is {referencedVersion}.");
        ModuleHook.discoveredNameToPath.Add(includedAssembly, fullPath);
        if (load)
        {
            Assembly.LoadFrom(fullPath);
            Log($"  Pre-loaded {includedAssembly.Name}.");
        }
        return true;
    }

    private static void Log(string msg)
    {
        msg = "[uTest Bootstrapper] " + msg;
        if (Dedicator.isStandaloneDedicatedServer)
        {
            CommandWindow.Log(msg);
        }
        else
        {
            UnturnedLog.info(msg);
        }
    }

    private static int VersionCompare(Version? v1, Version? v2)
    {
        if (v1 is null or { Build: 0, Major: 0, Minor: 0, Revision: 0 })
            return v2 is null or { Build: 0, Major: 0, Minor: 0, Revision: 0 } ? 0 : -1;
        if (v2 is null or { Build: 0, Major: 0, Minor: 0, Revision: 0 })
            return 1;

        return v1 == v2 ? 0 : v1 > v2 ? 1 : -1;
    }
}