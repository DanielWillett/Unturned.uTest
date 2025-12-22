using HarmonyLib;
using System;
using uTest.Module;

namespace uTest.Patches;

internal class UnturnedTestPatches(MainModule module) : IDisposable
{
    private readonly MainModule _module = module;
    private Harmony? _harmony;
    private List<Func<Harmony, bool>> _unpatches = null!;

    internal void Init()
    {
        _harmony = new Harmony("DanielWillett.uTest");
        _unpatches = new List<Func<Harmony, bool>>(8);

        RegisterPatch(Patches.SkipAddFoundAssetIfNotRequired.TryPatch, Patches.SkipAddFoundAssetIfNotRequired.TryUnpatch);
        RegisterPatch(Patches.ListenServerAddDummies.TryPatch, Patches.ListenServerAddDummies.TryUnpatch);
    }

    private void RegisterPatch(Func<Harmony, ILogger, bool> tryPatch, Func<Harmony, bool> tryUnpatch)
    {
        if (tryPatch(_harmony!, _module.Logger))
            _unpatches.Add(tryUnpatch);
    }

    public void Dispose()
    {
        if (_harmony == null)
            return;

        foreach (Func<Harmony, bool> unpatch in _unpatches)
            unpatch(_harmony);

        _harmony.UnpatchAll(_harmony.Id);
        _harmony = null;
    }
}