using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace uTest.Dummies.Host.Patches;

/// <summary>
/// Patches "SDG.Unturned.AssetsWorker.WorkerThreadState.AddFoundAsset" to skip assets that aren't supposed to be loaded.
/// </summary>
internal static class SkipAddFoundAssetIfNotRequired
{
    private const string PatchName = "AssetsWorker.WorkerThreadState.AddFoundAsset";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        Type? type = Type.GetType("SDG.Unturned.AssetsWorker+WorkerThreadState, Assembly-CSharp", throwOnError: false, ignoreCase: false);
        if (type == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find type."));
            return false;
        }

        _patchedMethod = type.GetMethod(
            "AddFoundAsset",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [ typeof(string), typeof(bool) ],
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, prefix: new HarmonyMethod(new Func<string, bool, bool>(Prefix).Method));
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
            harmony.Unpatch(_patchedMethod, new Func<string, bool, bool>(Prefix).Method);
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

    internal static bool Prefix(string filePath, bool checkForTranslations)
    {
        if (!File.Exists(filePath))
            return false;

        DummyPlayerHost mainModule = DummyPlayerHost.Instance;
        if (mainModule == null)
            return true;

        ILogger logger = mainModule.Logger;

        AssetLoadModel? model = mainModule.AssetLoadModel;
        bool shouldLoadAsset = model == null || model.Includes(filePath);
        if (false && logger.IsEnabled(LogLevel.Trace))
        {
            string fn;
            if (!filePath.EndsWith("Asset.dat"))
            {
                fn = Path.GetFileNameWithoutExtension(filePath);
            }
            else
            {
                string? dn = Path.GetDirectoryName(filePath);
                if (dn != null)
                    fn = Path.GetFileName(dn);
                else
                    fn = "Asset";
            }

            logger.LogTrace(string.Format(shouldLoadAsset ? Properties.Resources.LogTraceLoadingAsset : Properties.Resources.LogTraceSkippingAsset, fn));
        }

        return shouldLoadAsset;
    }
}