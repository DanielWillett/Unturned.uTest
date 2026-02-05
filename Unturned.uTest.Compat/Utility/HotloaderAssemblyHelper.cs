using System;
using System.IO;
using System.Reflection;

namespace uTest.Compat.Utility;

internal static class HotloaderAssemblyHelper
{
    private static Func<AssemblyName, Assembly?>? _findAssembly;
    private static Func<string, Assembly?>? _legacyGetAssembly;

    private static bool _isOpenModAbsent;

    /// <summary>
    /// Gets or loads an assembly by name, checking the openmod HotLoader first.
    /// </summary>
    internal static Assembly LoadAssemblyMaybeFromHotloader(AssemblyName assemblyName)
    {
        TryGetAssemblyFromHotloader(assemblyName, out Assembly hotLoadedAssembly);
        return hotLoadedAssembly ?? Assembly.Load(assemblyName);
    }

    internal static bool TryGetAssemblyFromHotloader(AssemblyName assemblyName, out Assembly assembly)
    {
        if (_isOpenModAbsent)
        {
            assembly = null!;
            return false;
        }

        if (_findAssembly == null && _legacyGetAssembly == null)
        {
            try
            {
                GetHotloaderMethod();
            }
            catch (FileNotFoundException)
            {
                _isOpenModAbsent = true;
            }
        }

        Assembly? asm = _findAssembly != null
            ? _findAssembly(assemblyName)
            : _legacyGetAssembly?.Invoke(assemblyName.FullName);

        if (asm == null)
        {
            assembly = null!;
            return false;
        }

        assembly = asm;
        return true;
    }

    private static void GetHotloaderMethod()
    {
        Assembly openmodCommon = Assembly.Load(
            new AssemblyName("OpenMod.Common, Version=3.6.0.0, Culture=neutral, PublicKeyToken=null")
        );

        Type hotLoader = openmodCommon.GetType("OpenMod.Common.Hotloading.Hotloader", throwOnError: true);

        MethodInfo? findAssemblyMethod = hotLoader.GetMethod("FindAssembly", BindingFlags.Public | BindingFlags.Static, null, [ typeof(AssemblyName) ], null);
        if (findAssemblyMethod != null)
        {
            _findAssembly = (Func<AssemblyName, Assembly?>)findAssemblyMethod.CreateDelegate(typeof(Func<AssemblyName, Assembly?>));
        }
        else
        {
            MethodInfo? getAssemblyMethod = hotLoader.GetMethod("GetAssembly", BindingFlags.Public | BindingFlags.Static, null, [ typeof(string) ], null);
            if (getAssemblyMethod != null)
            {
                _legacyGetAssembly = (Func<string, Assembly?>)getAssemblyMethod.CreateDelegate(typeof(Func<string, Assembly?>));
            }
            else
            {
                throw new MissingMethodException(hotLoader.FullName, "FindAssembly");
            }
        }
    }
}
