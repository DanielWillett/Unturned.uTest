using SDG.NetTransport.SystemSockets;
using System;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DanielWillett.ReflectionTools.Formatting;
using HarmonyLib;

namespace uTest.Patches;

/// <summary>
/// Patches "SDG.NetTransport.SystemSockets.ClientTransport_SystemSockets" and "SDG.NetTransport.SystemSockets.TransportConnection_SystemSocket" to replace the <see cref="SocketMessageLayer"/> with an instance of <see cref="SocketMessageLayerFixed"/>.
/// </summary>
internal static class SocketMessageLayerFix
{
    private const string ClientInitPatchName = "ClientTransport_SystemSockets.Initialize";
    private const string ServerInitPatchName = "TransportConnection_SystemSocket..ctor";
    
    private const string ClientUsagePatchName = "ClientTransport_SystemSockets.OnUpdate";
    private const string ServerUsagePatchName = "ServerTransport_SystemSockets.OnUpdate";

    private static bool _hasClientInitPatch;
    private static bool _hasServerInitPatch;

    private static bool _hasClientUsagePatch;
    private static bool _hasServerUsagePatch;

    private static MethodInfo? _patchedMethodClientInit;
    private static ConstructorInfo? _patchedMethodServerInit;

    private static MethodInfo? _patchedMethodClientUsage;
    private static MethodInfo? _patchedMethodServerUsage;
    
    internal static bool TryPatchClient(Harmony harmony, ILogger logger)
    {
        _hasClientInitPatch = false;
        _patchedMethodClientInit = typeof(ClientTransport_SystemSockets).GetMethod(
            "Initialize",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [ typeof(ClientTransportReady), typeof(ClientTransportFailure) ],
            null
        );
        if (_patchedMethodClientInit == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ClientInitPatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethodClientInit, transpiler: new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method));
            _hasClientInitPatch = true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ClientInitPatchName, "Patch error."), ex);
            return false;
        }

        if (TryPatchClientUsage(harmony, logger))
            return true;

        TryUnpatchClient(harmony);
        return false;
    }

    private static bool TryPatchClientUsage(Harmony harmony, ILogger logger)
    {
        _hasClientUsagePatch = false;
        _patchedMethodClientUsage = typeof(ClientTransport_SystemSockets).GetMethod(
            "OnUpdate",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            null
        );
        if (_patchedMethodClientUsage == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ClientUsagePatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethodClientUsage, transpiler: new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method));
            _hasClientUsagePatch = true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ClientUsagePatchName, "Patch error."), ex);
            return false;
        }

        return true;
    }

    internal static bool TryPatchServer(Harmony harmony, ILogger logger)
    {
        _hasServerInitPatch = false;
        Type? type = Type.GetType("SDG.NetTransport.SystemSockets.TransportConnection_SystemSocket, Assembly-CSharp", throwOnError: false, ignoreCase: false);
        if (type == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ServerInitPatchName, "Unable to find type."));
            return false;
        }

        _patchedMethodServerInit = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [ typeof(ServerTransport_SystemSockets), typeof(Socket) ],
            null
        );
        if (_patchedMethodServerInit == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ServerInitPatchName, "Unable to find target constructor."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethodServerInit, transpiler: new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method));
            _hasServerInitPatch = true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ServerInitPatchName, "Patch error."), ex);
            return false;
        }

        if (TryPatchServerUsage(harmony, logger))
            return true;

        TryUnpatchServer(harmony);
        return false;
    }

    private static bool TryPatchServerUsage(Harmony harmony, ILogger logger)
    {
        _hasServerUsagePatch = false;
        _patchedMethodServerUsage = typeof(ServerTransport_SystemSockets).GetMethod(
            "OnUpdate",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            null
        );
        if (_patchedMethodServerUsage == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ServerUsagePatchName, "Unable to find target method."));
            return false;
        }

        try
        {
            harmony.Patch(_patchedMethodServerUsage, transpiler: new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method));
            _hasServerUsagePatch = true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, ServerUsagePatchName, "Patch error."), ex);
            return false;
        }

        return true;
    }

    internal static bool TryUnpatchClient(Harmony harmony)
    {
        if (!_hasClientInitPatch)
            return false;

        try
        {
            harmony.Unpatch(_patchedMethodClientInit, new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _hasClientInitPatch = false;
            _patchedMethodClientInit = null;
        }
    }

    internal static bool TryUnpatchServer(Harmony harmony)
    {
        if (!_hasServerInitPatch)
            return false;

        try
        {
            harmony.Unpatch(_patchedMethodServerInit, new Func<IEnumerable<CodeInstruction>, MethodBase, ILGenerator, IEnumerable<CodeInstruction>>(Transpiler).Method);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _hasServerInitPatch = false;
            _patchedMethodServerInit = null;
        }
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        Type? socketLayerType = Type.GetType("SDG.NetTransport.SystemSockets.SocketMessageLayer, Assembly-CSharp", throwOnError: false);
        if (socketLayerType == null)
        {
            return ctx.Fail(new TypeDefinition("SocketMessageLayer"));
        }

        MethodInfo? originalRecvMtd = socketLayerType.GetMethod("ReceiveMessages", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (originalRecvMtd == null)
        {
            return ctx.Fail(new MethodDefinition("ReceiveMessages")
                .ReturningVoid()
                .WithParameter<Socket>("socket")
                .DeclaredIn(socketLayerType, isStatic: false)
            );
        }

        MethodInfo newRecvMtd = typeof(SocketMessageLayerFixed)
            .GetMethod(nameof(SocketMessageLayerFixed.ReceiveMessages_Fix), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        while (ctx.MoveNext())
        {
            if (ctx.Instruction.opcode == OpCodes.Newobj && ctx.Instruction.operand is ConstructorInfo ctor && ctor.DeclaringType == socketLayerType)
            {
                ctx.Replace(emit => emit.CreateObject<SocketMessageLayerFixed>());
            }
            else if (ctx.Instruction.Calls(originalRecvMtd))
            {
                ctx.Replace(emit => emit.Invoke(newRecvMtd));
            }
        }

        return ctx;
    }
}

internal class SocketMessageLayerFixed : SocketMessageLayer
{
    private byte _sizePart;
    private bool _hasSizePart;

    public void ReceiveMessages_Fix(Socket socket)
    {
        if (socket.Available <= 0)
            return;

        int bytesReceived = socket.Receive(internalBuffer, 0, internalBuffer.Length, SocketFlags.None, out SocketError errorCode);
        if (errorCode == SocketError.WouldBlock || errorCode != SocketError.Success || bytesReceived < 1)
            return;

        int offset = 0;
        while (offset < bytesReceived)
        {
            if (pendingMessage == null)
            {
                int size;
                if (_hasSizePart)
                {
                    size = (_sizePart << 8) | internalBuffer[offset];
                    _hasSizePart = false;
                    ++offset;
                }
                else if (offset == internalBuffer.Length - 1)
                {
                    _hasSizePart = true;
                    _sizePart = internalBuffer[offset];
                    break;
                }
                else
                {
                    size = (internalBuffer[offset] << 8) | internalBuffer[offset + 1];
                    offset += 2;
                }

                pendingMessage = new byte[size];
                pendingMessageOffset = 0;
            }
            else
            {
                int bytesLeft = bytesReceived - offset;
                int messageSize = pendingMessage.Length - pendingMessageOffset;
                if (bytesLeft < messageSize)
                {
                    Array.Copy(internalBuffer, offset, pendingMessage, pendingMessageOffset, bytesLeft);
                    pendingMessageOffset += bytesLeft;
                    offset += bytesLeft;
                }
                else
                {
                    Array.Copy(internalBuffer, offset, pendingMessage, pendingMessageOffset, messageSize);
                    offset += messageSize;
                    messageQueue.Enqueue(pendingMessage);
                    pendingMessage = null;
                }
            }
        }
    }
}