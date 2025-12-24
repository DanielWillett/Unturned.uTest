using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DanielWillett.ReflectionTools.Formatting;
using HarmonyLib;
using SDG.NetPak;
using System;
using System.Reflection;
using System.Reflection.Emit;

#pragma warning disable IDE0130

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.Unturned.ServerMessageHandler_ReadyToConnect.ReadMessage" to remove the 'rate limiter' for IP addresses, since all join requests come from the same IP address.
/// </summary>
internal static class RemoveReadyToConnectRateLimiter
{
    private const string PatchName = "ServerMessageHandler_ReadyToConnect.ReadMessage";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        Type? type = Type.GetType("SDG.Unturned.ServerMessageHandler_ReadyToConnect, Assembly-CSharp", throwOnError: false, ignoreCase: false);
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

        Type? tcRateLimiter = Type.GetType("SDG.Unturned.TransportConnectionRateLimiter, Assembly-CSharp");
        if (tcRateLimiter == null)
        {
            return ctx.Fail(new TypeDefinition("TransportConnectionRateLimiter"));
        }

        MethodInfo? isBlockedMethod = tcRateLimiter.GetMethod(
            "IsBlockedByAddressRateLimiting",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [ typeof(uint) ],
            null
        );

        if (isBlockedMethod == null)
        {
            return ctx.Fail(
                new MethodDefinition("IsBlockedByAddressRateLimiting")
                    .WithParameter<uint>("connectionAddress")
                    .DeclaredIn(tcRateLimiter, false)
                    .Returning<bool>()
            );
        }

        FieldInfo? rateLimiterField = method.DeclaringType!.GetField("joinRateLimiter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (rateLimiterField == null || rateLimiterField.FieldType != tcRateLimiter)
        {
            return ctx.Fail(
                new FieldDefinition("joinRateLimiter")
                    .WithFieldType(tcRateLimiter)
                    .DeclaredIn(method.DeclaringType!, true)
            );
        }

        bool patched = false;
        while (ctx.MoveNext())
        {
            if (ctx.Instruction.opcode != OpCodes.Ldsfld || (FieldInfo?)ctx.Instruction.operand != rateLimiterField)
                continue;

            int index = ctx.CaretIndex;
            while (ctx.MoveNext() && !ctx.Instruction.opcode.IsBrAny()) ;
            if (ctx.CaretIndex >= ctx.Count)
                break;

            if (!ctx[ctx.CaretIndex - 1].Calls(isBlockedMethod))
                continue;

            int ct = ctx.CaretIndex - index + 1;
            Label dest = (Label)ctx.Instruction.operand;
            bool isBrTrue = ctx.Instruction.opcode.IsBr(brtrue: true);
            ctx.CaretIndex = index;
            ctx.Replace(ct, emit =>
            {
                if (isBrTrue)
                {
                    // if (!IsBlocked) ...
                    emit.NoOperation();
                }
                else
                {
                    // if (IsBlocked) ...
                    emit.Branch(dest);
                }
            });

            patched = true;
            break;
        }

        if (!patched)
        {
            return ctx.Fail("Unable to remove IP address rate limiter from ReadyToConnect message handler.");
        }

        ctx.LogInfo("Removed IP address rate limiter from ReadyToConnect message handler.");
        return ctx;
    }
}

#pragma warning restore IDE0130