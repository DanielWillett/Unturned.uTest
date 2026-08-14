using HarmonyLib;
using System;
using System.Reflection;

namespace uTest.Patches;

internal class UnturnedLogClientHook
{
    private const string PatchName = "Logs.printLine";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static Action<string>? OnLog;

    internal static bool TryPatch(Harmony harmony, Logging.ILogger logger)
    {
        _hasPatch = false;

        _patchedMethod = typeof(Logs).GetMethod(
            "printLine",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            [ typeof(string) ],
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, postfix: new HarmonyMethod(new Action<string>(Postfix).Method));
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
            harmony.Unpatch(_patchedMethod, new Action<string>(Postfix).Method);
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

    private static void Postfix(string message)
    {
        OnLog?.Invoke(message);
    }
}
