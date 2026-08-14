using HarmonyLib;
using System;
using System.Reflection;
using uTest.Module;

namespace uTest.Patches;

internal class DelayDedicatedUgcUntilTestsLoaded
{
    private const string PatchName = "Provider.initializeDedicatedUGC";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, Logging.ILogger logger)
    {
        _hasPatch = false;

        _patchedMethod = typeof(Provider).GetMethod(
            "initializeDedicatedUGC",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, prefix: new HarmonyMethod(new Func<bool>(Prefix).Method));
            _hasPatch = true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Patch error."), ex);
            return false;
        }

        return true;
    }

    internal static bool TryUnpatch(Harmony harmony)
    {
        if (!_hasPatch)
            return false;

        try
        {
            harmony.Unpatch(_patchedMethod, new Func<bool>(Prefix).Method);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _hasPatch = false;
            _patchedMethod = null;
        }
    }

    private static bool Prefix()
    {
        MainModule.Instance.HasTriedToLoadUgc = true;
        return MainModule.Instance.HasDiscoveredRequiredUgc;
    }
}
