using HarmonyLib;
using System;
using System.Reflection;

namespace uTest.Dummies.Host.Patches;

// ReSharper disable InconsistentNaming

/// <summary>
/// Patches "SDG.Unturned.LocalHwid.GetHwids" to return the given HWIDs if specified by the server.
/// </summary>
internal static class ChangeHardwareIDs
{
    private const string PatchName = "LocalHwid.GetHwids";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        _patchedMethod = typeof(LocalHwid).GetMethod(
            "GetHwids",
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
            harmony.Patch(_patchedMethod, prefix: new HarmonyMethod(new PrefixHandle(Prefix).Method));
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
            harmony.Unpatch(_patchedMethod, new PrefixHandle(Prefix).Method);
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

    private delegate bool PrefixHandle(ref byte[][] __result);
    internal static bool Prefix(ref byte[][] __result)
    {
        byte[]? hwids = DummyPlayerHost.Instance.HWIDs;
        if (hwids == null || hwids.Length % 20 != 0)
            return true;

        int hwidCt = hwids.Length / 20;
        if (hwidCt > 8)
            return true;

        byte[][] hwidList = new byte[hwidCt][];
        
        for (int i = 0; i < hwidList.Length; ++i)
        {
            byte[] hwid = new byte[20];
            Buffer.BlockCopy(hwids, i * 20, hwid, 0, 20);
            hwidList[i] = hwid;
        }

        __result = hwidList;
        return false;
    }
}