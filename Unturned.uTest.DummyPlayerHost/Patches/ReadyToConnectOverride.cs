using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using HarmonyLib;
using SDG.NetPak;
using SDG.Provider;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using uTest.Patches;

namespace uTest.Dummies.Host.Patches;

// ReSharper disable InconsistentNaming

/// <summary>
/// Patches "SDG.Unturned.LocalHwid.GetHwids" to return the given HWIDs if specified by the server.
/// </summary>
internal static class ReadyToConnectOverride
{
    private const string PatchName = "Provider.onLevelLoaded (ReadyToConnect)";
    private static bool _hasPatch;

    private static MethodInfo? _patchedMethod;

    internal static bool TryPatch(Harmony harmony, ILogger logger)
    {
        _hasPatch = false;
        Type[] allNestedTypes = typeof(Provider).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        
        _patchedMethod = allNestedTypes
            .SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .FirstOrDefault(x => x.Name.Contains("onLevelLoaded", StringComparison.Ordinal)
                                 && x.ReturnType == typeof(void)
                                 && x.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(NetPakWriter)
            );
        
        if (_patchedMethod == null)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorPatchFailed, PatchName, "Unable to find lambda method."));
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

    private static ReadyToConnectInfo GetReadyToConnectInfo() => DummyPlayerHost.Instance?.ReadyToConnectInfo ?? ReadyToConnectInfo.Default;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        MethodInfo writePlatformMethod = new Func<NetPakWriter, EClientPlatform, bool>(EClientPlatform_NetEnum.WriteEnum).Method;
        MethodInfo writeUInt16Method = new Func<NetPakWriter, ushort, bool>(SystemNetPakWriterEx.WriteUInt16).Method;
        MethodInfo sbToString = new Func<string>(new StringBuilder().ToString).Method;

        MethodInfo getInfo = new Func<ReadyToConnectInfo>(GetReadyToConnectInfo).Method;
        MethodInfo getModules = typeof(ReadyToConnectInfo).GetMethod(nameof(ReadyToConnectInfo.GetRequiredModules),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo initDefaults = typeof(ReadyToConnectInfo).GetMethod(nameof(ReadyToConnectInfo.InitializeDefaults),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        MethodInfo? getGameVersion = typeof(Provider).GetProperty(nameof(Provider.APP_VERSION_PACKED), typeof(uint))?.GetMethod;
        if (getGameVersion == null)
        {
            ctx.LogWarning("Failed to locate Provider.APP_VERSION_PACKED getter.");
        }

        MethodInfo? getIsPro = typeof(Provider).GetProperty(nameof(Provider.isPro), typeof(bool))?.GetMethod;
        if (getIsPro == null)
        {
            ctx.LogWarning("Failed to locate Provider.isPro getter.");
        }

        MethodInfo? getPingMs = typeof(SteamServerAdvertisement).GetProperty(nameof(SteamServerAdvertisement.PingMs), typeof(int))?.GetMethod;
        if (getPingMs == null)
        {
            ctx.LogWarning("Failed to locate SteamServerAdvertisement.PingMs getter.");
        }

        MethodInfo? getLanguage = typeof(Provider).GetProperty(nameof(Provider.language), typeof(string))?.GetMethod;
        if (getLanguage == null)
        {
            ctx.LogWarning("Failed to locate Provider.language getter.");
        }

        FieldInfo? moduleBuilderField = typeof(Provider).GetField("modBuilder", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (moduleBuilderField == null)
        {
            ctx.LogWarning("Failed to locate Provider.modBuilder field.");
        }

        MethodInfo? getLobby = typeof(Lobbies).GetProperty(nameof(Lobbies.currentLobby), typeof(CSteamID))?.GetMethod;
        if (getLobby == null)
        {
            ctx.LogWarning("Failed to locate Lobbies.currentLobby getter.");
        }

        MethodInfo? getLevelVersion = typeof(Level).GetProperty(nameof(Level.packedVersion), typeof(uint))?.GetMethod;
        if (getLevelVersion == null)
        {
            ctx.LogWarning("Failed to locate Level.packedVersion getter.");
        }

        MethodInfo? getLevelHash = typeof(Level).GetProperty(nameof(Level.hash), typeof(byte[]))?.GetMethod;
        if (getLevelHash == null)
        {
            ctx.LogWarning("Failed to locate Level.hash getter.");
        }

        MethodInfo? getAssemblyHash = typeof(ReadWrite).GetMethod(nameof(ReadWrite.readData), Type.EmptyTypes);
        if (getAssemblyHash == null)
        {
            ctx.LogWarning("Failed to locate ReadWrite.readData() method.");
        }

        FieldInfo? getResourceHash = typeof(ResourceHash).GetField(nameof(ResourceHash.localHash));
        if (getResourceHash == null)
        {
            ctx.LogWarning("Failed to locate ResourceHash.localHash field.");
        }

        MethodInfo? getEconHash = typeof(TempSteamworksEconomy).GetProperty(nameof(TempSteamworksEconomy.econInfoHash), typeof(byte[]))?.GetMethod;
        if (getEconHash == null)
        {
            ctx.LogWarning("Failed to locate TempSteamworksEconomy.econInfoHash getter.");
        }

        LocalBuilder connectInfoLcl = generator.DeclareLocal(typeof(ReadyToConnectInfo));

        int patches = 0;

        bool isWaitingForWritePing = false;
        bool isWaitingForMoudleBuilder = false;
        bool hasEmittedLocal = false;
        while (ctx.MoveNext())
        {
            if (!hasEmittedLocal)
            {
                hasEmittedLocal = true;
                ctx.EmitAbove(emit =>
                {
                    emit.Invoke(getInfo)
                        .Duplicate()
                        .SetLocalValue(connectInfoLcl)
                        .Invoke(initDefaults);
                });
                ++patches;
            }

            if (isWaitingForWritePing && ctx.Instruction.Calls(writeUInt16Method))
            {
                isWaitingForWritePing = false;
                ctx.EmitAbove(emit =>
                {
                    emit.PopFromStack()
                        .LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, ushort>(x => x.ReportedPing);
                });
                ++patches;
            }
            else if (isWaitingForMoudleBuilder && ctx.Instruction.Calls(sbToString))
            {
                isWaitingForMoudleBuilder = false;
                ctx.EmitBelow(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .Invoke(getModules);
                });
                ++patches;
            }
            else if (ctx.Instruction.Calls(writePlatformMethod))
            {
                ctx.EmitAbove(emit =>
                {
                    emit.PopFromStack()
                        .LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, EClientPlatform>(x => x.Platform);
                });
                ++patches;
            }
            else if (getGameVersion != null && ctx.Instruction.Calls(getGameVersion))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, uint>(x => x.GameVersion);
                });
                ++patches;
            }
            else if (getIsPro != null && ctx.Instruction.Calls(getIsPro))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, bool>(x => x.IsGold);
                });
                ++patches;
            }
            else if (getPingMs != null && ctx.Instruction.Calls(getPingMs))
            {
                isWaitingForWritePing = true;
                ++patches;
            }
            else if (getLanguage != null && ctx.Instruction.Calls(getLanguage))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, string>(x => x.Language);
                });
                ++patches;
            }
            else if (moduleBuilderField != null && ctx.Instruction.LoadsField(moduleBuilderField))
            {
                isWaitingForMoudleBuilder = true;
                ++patches;
            }
            else if (getLobby != null && ctx.Instruction.Calls(getLobby))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, CSteamID>(x => x.LobbyId);
                });
                ++patches;
            }
            else if (getLevelVersion != null && ctx.Instruction.Calls(getLevelVersion))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, uint>(x => x.LevelVersion);
                });
                ++patches;
            }
            else if (getLevelHash != null && ctx.Instruction.Calls(getLevelHash))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, byte[]>(x => x.LevelHash!);
                });
                ++patches;
            }
            else if (getAssemblyHash != null && ctx.Instruction.Calls(getAssemblyHash))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, byte[]>(x => x.AssemblyHash!);
                });
                ++patches;
            }
            else if (getResourceHash != null && ctx.Instruction.LoadsField(getResourceHash))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, byte[]>(x => x.ResourceHash!);
                });
                ++patches;
            }
            else if (getEconHash != null && ctx.Instruction.Calls(getEconHash))
            {
                ctx.Replace(emit =>
                {
                    emit.LoadLocalValue(connectInfoLcl)
                        .LoadFieldValue<ReadyToConnectInfo, byte[]>(x => x.EconHash!);
                });
                ++patches;
            }
        }

        if (patches != 15)
        {
            ctx.LogWarning("Didn't patch exactly 15 lines, may be broken.");
        }

        return ctx;
    }
}

internal sealed class ReadyToConnectInfo
{
    public static ReadyToConnectInfo Default => new ReadyToConnectInfo
    {
        Platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? EClientPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? EClientPlatform.Mac
            : EClientPlatform.Linux,
        GameVersion = Provider.APP_VERSION_PACKED,
        IsGold = Provider.isPro,
        ReportedPing = 0,
        Language = Provider.language,
        Modules = null,
        LobbyId = Lobbies.currentLobby
    };

    public bool MixupLevelHash;
    public bool MixupAssemblyHash;
    public bool MixupResourceHash;
    public bool MixupEconHash;
    public uint? OverrideLevelVersion;
    public bool MixupLevelVersion;
    public required EClientPlatform Platform;
    public required uint GameVersion;
    public required bool IsGold;
    public required ushort ReportedPing;
    public required string Language;
    public required string? Modules;
    public required CSteamID LobbyId;
    public uint LevelVersion;
    public byte[]? LevelHash;
    public byte[]? AssemblyHash;
    public byte[]? ResourceHash;
    public byte[]? EconHash;

    public ulong Shirt;
    public ulong Pants;
    public ulong Hat;
    public ulong Backpack;
    public ulong Vest;
    public ulong Mask;
    public ulong Glasses;
    public ulong[]? Skins;

    internal static string GetRequiredModules(string original, ReadyToConnectInfo info)
    {
        return info.Modules ?? original;
    }

    [MemberNotNull(nameof(LevelHash))]
    [MemberNotNull(nameof(AssemblyHash))]
    [MemberNotNull(nameof(ResourceHash))]
    [MemberNotNull(nameof(EconHash))]
    internal void InitializeDefaults()
    {
        LevelHash = Level.hash;
        AssemblyHash = ReadWrite.readData();
        ResourceHash = SDG.Unturned.ResourceHash.localHash;
        EconHash = TempSteamworksEconomy.econInfoHash;

        if (MixupLevelHash)
            BreakHash(ref LevelHash);
        if (MixupAssemblyHash)
            BreakHash(ref AssemblyHash);
        if (MixupResourceHash)
            BreakHash(ref ResourceHash);
        if (MixupEconHash)
            BreakHash(ref EconHash);

        LevelVersion = Level.packedVersion;
        if (OverrideLevelVersion.HasValue)
            LevelVersion = OverrideLevelVersion.Value;
        else if (MixupLevelVersion)
            --LevelVersion;

        Characters.active.packageShirt = Shirt;
        Characters.active.packagePants = Pants;
        Characters.active.packageHat = Hat;
        Characters.active.packageBackpack = Backpack;
        Characters.active.packageVest = Vest;
        Characters.active.packageMask = Mask;
        Characters.active.packageGlasses = Glasses;

        Characters.packageSkins.Clear();
        if (Skins is { Length: > 0 })
            Characters.packageSkins.AddRange(Skins);
    }

    private static void BreakHash(ref byte[] input)
    {
        // shift left
        byte[] newarr = new byte[input.Length];
        newarr[^1] = input[0];
        for (int i = 0; i < newarr.Length - 1; ++i)
        {
            newarr[i] = input[i + 1];
        }

        input = newarr;
    }
}