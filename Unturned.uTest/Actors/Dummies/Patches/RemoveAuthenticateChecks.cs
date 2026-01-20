using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using HarmonyLib;
using SDG.NetPak;
using System;
using System.Reflection;
using System.Reflection.Emit;
using uTest.Module;
using uTest.Util;

#pragma warning disable IDE0130

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.Unturned.ServerMessageHandler_Authenticate.ReadMessage" to remove the 'rate limiter' for IP addresses, since all join requests come from the same IP address.
/// It also removes the group ID authentication.
/// </summary>
internal static class RemoveAuthenticateChecks
{
    private const string PatchName = "ServerMessageHandler_Authenticate.ReadMessage";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        Type? type = Type.GetType("SDG.Unturned.ServerMessageHandler_Authenticate, Assembly-CSharp", throwOnError: false, ignoreCase: false);
        if (type == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find type."));
            return false;
        }

        _patchedMethod = type.GetMethod(
            "ReadMessage",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            CallingConventions.Any,
            [ typeof(ITransportConnection), typeof(NetPakReader) ],
            null
        );
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethod, transpiler: new HarmonyMethod(new TranspilerSignature(Transpiler).Method));
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
            harmony.Unpatch(_patchedMethod, new TranspilerSignature(Transpiler).Method);
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

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        FieldInfo? hasGroupField = typeof(SteamPending).GetField(nameof(SteamPending.hasGroup), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (hasGroupField == null)
        {
            ctx.LogWarning("Unable to locate field 'SteamPending.hasGroup'. Setting Group ID may fail unless the test runner is in the group.");
        }

        MethodInfo? equalityOp = Operators.Find(typeof(CSteamID), OperatorType.Equality);
        if (equalityOp == null)
        {
            ctx.LogWarning("Unable to locate equality operator on 'CSteamID'. Setting Group ID may fail unless the test runner is in the group.");
        }

        bool patchedGroup = false;
        while (ctx.MoveNext())
        {
            if (patchedGroup || ctx.Instruction.opcode != OpCodes.Stfld || (FieldInfo?)ctx.Instruction.operand != hasGroupField || equalityOp == null)
            {
                continue;
            }

            // skip group validation
            if (!ctx.MoveNext())
                break;
            if (!ctx.Instruction.opcode.IsBrAny())
                continue;

            int rtnIndex = ctx.CaretIndex;

            while (ctx.MoveBack())
            {
                if (!ctx.Instruction.Calls(equalityOp))
                    continue;

                int ind = ctx.CaretIndex;
                LocalReference lcl = default;
                while (ctx.MoveBack())
                {
                    if (!ctx.Instruction.opcode.IsLdLoc())
                        continue;

                    lcl = ctx.Instruction.GetLocalReference();
                    break;
                }

                ctx.CaretIndex = ind;

                if (!ctx.MoveNext() || lcl.Local == null && lcl.Index < 0 || !ctx.Instruction.opcode.IsBrAny())
                    break;

                Label brDst = (Label)ctx.Instruction.operand;
                ctx.EmitBelow(emit =>
                {
                    emit.LoadLocalValue(lcl)
                        .Invoke(new Func<SteamPending, bool>(IsDummyStub).Method)
                        .BranchIfTrue(brDst);
                });
                patchedGroup = true;
                ctx.LogInfo("Removed Steam group validation from ReadyToConnect message handler.");
                break;
            }

            ctx.CaretIndex = rtnIndex;
        }

        if (!patchedGroup)
        {
            ctx.LogWarning("Unable to remove Steam group validation from ReadyToConnect message handler.");
        }

        return ctx;
    }

    private static bool IsDummyStub(SteamPending pending)
    {
        return MainModule.Instance.Dummies.TryGetDummy(pending.playerID.steamID.m_SteamID, out _);
    }
}

#pragma warning restore IDE0130