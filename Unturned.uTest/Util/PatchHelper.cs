using DanielWillett.ReflectionTools;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Reflection;
using System.Reflection.Emit;
using DanielWillett.ReflectionTools.Formatting;

namespace uTest.Util;

internal static class PatchHelper
{
#if DEBUG
    /// <summary>
    /// Transpiles a method to add logging for entry and exit of the method.
    /// </summary>
    internal static bool AddFunctionIOLogging(Harmony harmony, MethodBase method)
    {
        if (method == null)
        {
            UnturnedLog.error("PatchHelpers.AddFunctionIOLogging", "Error adding function IO logging to method, not found.");
            return false;
        }
        try
        {
            harmony.Patch(method,
                transpiler: new HarmonyMethod(typeof(PatchHelper).GetMethod(nameof(AddFunctionIOTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            UnturnedLog.info("PatchHelpers.AddFunctionIOLogging", $"Added function IO logging to: {Accessor.Formatter.Format(method)}.");
            return true;
        }
        catch (Exception ex)
        {
            UnturnedLog.error("PatchHelpers.AddFunctionIOLogging", ex, $"Error adding function IO logging to {Accessor.Formatter.Format(method)}.");
            return false;
        }
    }


    /// <summary>
    /// Transpiles a method to add logging for each instruction.
    /// </summary>
    internal static bool AddFunctionStepthrough(Harmony harmony, MethodBase method)
    {
        if (method == null)
        {
            UnturnedLog.error("PatchHelpers.AddFunctionStepthrough", "Error adding function stepthrough to method, not found.");
            return false;
        }
        try
        {
            harmony.Patch(method,
                transpiler: new HarmonyMethod(typeof(PatchHelper).GetMethod(nameof(AddFunctionStepthroughTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            UnturnedLog.info("PatchHelpers.AddFunctionStepthrough", $"Added stepthrough to: {Accessor.Formatter.Format(method)}.");
            return true;
        }
        catch (Exception ex)
        {
            UnturnedLog.error("PatchHelpers.AddFunctionStepthrough", ex, $"Error adding function stepthrough to {Accessor.Formatter.Format(method)}.");
            return false;
        }
    }

    private static readonly MethodInfo LogInfo = new Action<object>(UnturnedLog.info).Method;

    private static IEnumerable<CodeInstruction> AddFunctionIOTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "In method: " + Accessor.Formatter.Format(method) + " (basic entry)");
        yield return new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo);

        foreach (CodeInstruction instr in instructions)
        {
            if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw)
            {
                CodeInstruction logInstr = new CodeInstruction(OpCodes.Ldstr, $"Out method: {Accessor.Formatter.Format(method)}{(instr.opcode == OpCodes.Ret ? " (returned)" : " (exception)")}");
                logInstr.WithStartBlocksFrom(instr);
                yield return logInstr;
                yield return new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo);
            }
            yield return instr;
        }
    }

    private static IEnumerable<CodeInstruction> AddFunctionStepthroughTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> ins = [.. instructions];
        AddFunctionStepthrough(ins, method);
        return ins;
    }
    private static void AddFunctionStepthrough(List<CodeInstruction> ins, MethodBase method)
    {
        ins.Insert(0, new CodeInstruction(OpCodes.Ldstr, "Stepping through Method: " + Accessor.Formatter.Format(method) + ":"));
        ins.Insert(1, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
        ins[0].WithStartBlocksFrom(ins[2]);
        for (int i = 2; i < ins.Count; i++)
        {
            CodeInstruction instr = ins[i];
            CodeInstruction? start = null;

            CodeInstruction mainInst = new CodeInstruction(OpCodes.Ldstr, "  " + PatchUtility.CodeInstructionFormatter.FormatCodeInstruction(instr, OpCodeFormattingContext.List).Replace("\n", "\n[XXXX-XX-XX XX:XX:XX] "));
            start ??= mainInst;
            ins.Insert(i, mainInst);
            ins.Insert(i + 1, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
            i += 2;

            start.WithStartBlocksFrom(instr);
        }
    }
#endif
}