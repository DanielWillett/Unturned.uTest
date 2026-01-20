using HarmonyLib;
using System;
using System.Reflection;
using uTest.Dummies;
using uTest.Module;

#pragma warning disable IDE0130

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.Unturned.DedicatedUGC.installNextItem" to update dummies when a new workshop item is installed.
/// </summary>
internal static class WorkshopItemsQueriedUpdateDummies
{
    private const string PatchName = "DedicatedUGC.installNextItem";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        _patchedMethod = typeof(DedicatedUGC).GetMethod(
            "installNextItem",
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
            harmony.Patch(_patchedMethod, prefix: new HarmonyMethod(new Action(Prefix).Method));
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
            harmony.Unpatch(_patchedMethod, new Action(Prefix).Method);
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

    internal static void Prefix()
    {
        DummyManager dummyManager = MainModule.Instance.Dummies;
        if (dummyManager.RemoteDummies is { } launcher)
        {
            launcher.NotifyWorkshopUpdate(DedicatedUGC.itemsToDownload.ToArray());
        }
    }
}