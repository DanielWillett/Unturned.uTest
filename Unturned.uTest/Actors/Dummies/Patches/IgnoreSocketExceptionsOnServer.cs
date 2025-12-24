using HarmonyLib;
using System;
using System.Reflection;

#pragma warning disable IDE0130

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.NetTransport.SystemSockets.TransportConnection_SystemSocket.Send" to prevent exceptions from escaping the send method, instead just dismissing the client.
/// </summary>
internal static class IgnoreSocketExceptionsOnServer
{
    private const string PatchName = "TransportConnection_SystemSocket.Send";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        Type? type = Type.GetType("SDG.NetTransport.SystemSockets.TransportConnection_SystemSocket, Assembly-CSharp", throwOnError: false, ignoreCase: false);
        if (type == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find type."));
            return false;
        }

        _patchedMethod = type.GetMethod(
            "Send",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [ typeof(byte[]), typeof(long), typeof(ENetReliability) ],
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, finalizer: new HarmonyMethod(new FinalizerDelegate(Finalizer).Method));
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
            harmony.Unpatch(_patchedMethod, new FinalizerDelegate(Finalizer).Method);
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

    private delegate Exception? FinalizerDelegate(Exception? __exception, ITransportConnection __instance);

    internal static Exception? Finalizer(Exception? __exception, ITransportConnection __instance)
    {
        if (__exception == null)
            return null;


        UnturnedLog.warn("Exception sending message to player.");
        UnturnedLog.warn(__exception);

        UnturnedLog.info("Dismissing next frame...");
        Task.Run(async () =>
        {
            await GameThread.Switch();
            Provider.dismiss(Provider.findTransportConnectionSteamId(__instance));
        });
        return null;
    }
}

#pragma warning restore IDE0130