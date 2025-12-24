using DanielWillett.ReflectionTools;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace uTest.Patches;

internal delegate IEnumerable<CodeInstruction> TranspilerSignature(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method);

internal class UnturnedTestPatches(ILogger logger) : IDisposable
{
    private readonly ILogger _logger = logger;
    private Harmony? _harmony;
    private List<Func<Harmony, bool>> _unpatches = null!;

    internal void Init(string logDir, Action<UnturnedTestPatches> register)
    {
        HarmonyLog.ResetConditional(Path.Combine(logDir, "harmony.log"));

        _harmony = new Harmony("DanielWillett.uTest");
        _unpatches = new List<Func<Harmony, bool>>(8);

        register(this);
    }

    public void RegisterPatch(Func<Harmony, ILogger, bool> tryPatch, Func<Harmony, bool> tryUnpatch, bool critical = false)
    {
        if (tryPatch(_harmony!, _logger))
        {
            _logger.LogDebug($"Applied patch: {Accessor.Formatter.Format(tryPatch.Method.DeclaringType!)}.");
            _unpatches.Add(tryUnpatch);
        }
        else if (critical)
        {
            Dispose();
            throw new Exception($"Failed to apply patch {Accessor.Formatter.Format(tryPatch.Method.DeclaringType!)}.");
        }
    }

    public void Dispose()
    {
        if (_harmony == null)
            return;

        foreach (Func<Harmony, bool> unpatch in _unpatches)
            unpatch(_harmony);

        _unpatches.Clear();
        _harmony.UnpatchAll(_harmony.Id);
        _harmony = null;
    }
}