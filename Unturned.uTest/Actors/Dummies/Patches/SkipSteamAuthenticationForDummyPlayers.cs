using HarmonyLib;
using System;
using System.Reflection;
using uTest.Module;

#pragma warning disable IDE0130

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.Unturned.Provider.verifyTicket" to skip running steam authentication and skip joining a group.
/// </summary>
internal static class SkipSteamAuthenticationForDummyPlayers
{
    private const string PatchName = "Provider.verifyTicket";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        _patchedMethod = typeof(Provider).GetMethod(
            "verifyTicket",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            [ typeof(CSteamID), typeof(byte[]) ],
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, prefix: new PrefixDelegate(Prefix).Method);
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
            harmony.Unpatch(_patchedMethod, new PrefixDelegate(Prefix).Method);
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

    private delegate bool PrefixDelegate(CSteamID steamID, byte[] ticket, ref bool __result);
    internal static bool Prefix(CSteamID steamID, byte[] ticket, ref bool __result)
    {
        if (!MainModule.Instance.Dummies.TryGetDummy(steamID.m_SteamID, out _))
        {
            return true;
        }

        MainModule.Instance.Logger.LogTrace($"Skipping steam authentication for dummy {steamID}.");

        ulong s64 = steamID.m_SteamID;
        SteamPending pending = Provider.pending.Find(x => x.playerID.steamID.m_SteamID == s64);
        if (pending == null)
        {
            return true;
        }

        pending.assignedPro = pending.isPro;
        pending.playerID.group = CSteamID.Nil;
        pending.hasAuthentication = true;
        __result = true;
        return false;
    }
}

#pragma warning restore IDE0130