using HarmonyLib;
using System;
using System.Reflection;

namespace uTest.Dummies.Host.Patches;


/// <summary>
/// Patches "SDG.Unturned.ConvenientSavedata.save" and "SDG.Unturned.ConvenientSavedata.SaveIfDirty" to prevent convenient-savedata from saving (causing sharing violations).
/// </summary>
internal static class DisableConvenientSavedata
{
    private const string PatchName = "ConvenientSavedata.save/SaveIfDirty";
    private static bool _hasPatchSave;
    private static bool _hasPatchSaveIfDirty;

    private static MethodInfo? _patchedMethodSave;
    private static MethodInfo? _patchedMethodSaveIfDirty;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatchSave = false;
        _hasPatchSaveIfDirty = false;

        _patchedMethodSave = typeof(ConvenientSavedata).GetMethod(
            "save",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            null
        );

        _patchedMethodSaveIfDirty = typeof(ConvenientSavedata).GetMethod(
            "SaveIfDirty",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            null
        );
        if (_patchedMethodSave == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method (save)."));
        }
        if (_patchedMethodSaveIfDirty == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method (SaveIfDirty)."));
        }
        if (_patchedMethodSave == null && _patchedMethodSaveIfDirty == null)
        {
            return false;
        }

        if (_patchedMethodSave != null)
        {
            try
            {
                harmony.Patch(_patchedMethodSave, prefix: new HarmonyMethod(new Func<bool>(Prefix).Method));
                _hasPatchSave = true;
            }
            catch (Exception ex)
            {
                logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Patch error (save)."), ex);
                return false;
            }
        }

        if (_patchedMethodSaveIfDirty != null)
        {
            try
            {
                harmony.Patch(_patchedMethodSaveIfDirty, prefix: new HarmonyMethod(new Func<bool>(Prefix).Method));
                _hasPatchSaveIfDirty = true;
            }
            catch (Exception ex)
            {
                logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Patch error (SaveIfDirty)."), ex);
                return false;
            }
        }

        return true;
    }

    internal static bool TryUnpatch(Harmony harmony)
    {
        if (!_hasPatchSave && !_hasPatchSaveIfDirty)
            return false;

        bool s = true;
        if (_hasPatchSave)
        {
            try
            {
                harmony.Unpatch(_patchedMethodSave, new Func<bool>(Prefix).Method);
            }
            catch
            {
                s = false;
            }
            finally
            {
                _hasPatchSave = false;
                _patchedMethodSave = null;
            }
        }

        if (_hasPatchSaveIfDirty)
        {
            try
            {
                harmony.Unpatch(_patchedMethodSaveIfDirty, new Func<bool>(Prefix).Method);
            }
            catch
            {
                s = false;
            }
            finally
            {
                _hasPatchSaveIfDirty = false;
                _patchedMethodSaveIfDirty = null;
            }
        }

        return s;
    }

    internal static bool Prefix()
    {
        UnturnedLog.info("uTest: Skipped saving convenient data");
        return false;
    }
}