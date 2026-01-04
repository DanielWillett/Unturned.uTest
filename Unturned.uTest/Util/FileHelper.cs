using SDG.Framework.Foliage;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace uTest;

internal static class FileHelper
{
    private static readonly Dictionary<Type, int> CachedAssetTypes;

    // wtf microsoft
    private static readonly DateTime FileNotExistsWriteTimeReturnValue = new DateTime(1601, 01, 01, 00, 00, 00);

    public static DateTime GetLastWriteTimeUTCSafe(string file, DateTime defaultValue)
    {
        DateTime dt;
        try
        {
            dt = File.GetLastWriteTimeUtc(file);
        }
        catch
        {
            return defaultValue;
        }

        return dt == FileNotExistsWriteTimeReturnValue ? defaultValue : dt;
    }

    public static DateTime GetLastWriteTimeUTCSafe(Assembly assembly, DateTime defaultValue)
    {
        DateTime dt;
        try
        {
            dt = File.GetLastWriteTimeUtc(assembly.Location);
        }
        catch
        {
            return defaultValue;
        }

        return dt == FileNotExistsWriteTimeReturnValue ? defaultValue : dt;
    }

    static FileHelper()
    {
        if (Type.GetType("SDG.Unturned.Provider, Assembly-CSharp", throwOnError: false, ignoreCase: false) != null)
        {
            CachedAssetTypes = GetAssetTypeCache();
        }
        else
        {
            CachedAssetTypes = new Dictionary<Type, int>();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Dictionary<Type, int> GetAssetTypeCache()
    {
        return new Dictionary<Type, int>
        {
            { typeof(AnimalAsset), (int)EAssetType.ANIMAL },
            { typeof(DialogueAsset), (int)EAssetType.NPC },
            { typeof(EffectAsset), (int)EAssetType.EFFECT },
            { typeof(ItemAsset), (int)EAssetType.ITEM },
            { typeof(MythicAsset), (int)EAssetType.MYTHIC },
            { typeof(ObjectAsset), (int)EAssetType.OBJECT },
            { typeof(QuestAsset), (int)EAssetType.NPC },
            { typeof(ResourceAsset), (int)EAssetType.RESOURCE },
            { typeof(SkinAsset), (int)EAssetType.SKIN },
            { typeof(SpawnAsset), (int)EAssetType.SPAWN },
            { typeof(VehicleAsset), (int)EAssetType.VEHICLE },
            { typeof(VehicleRedirectorAsset), (int)EAssetType.VEHICLE },
            { typeof(VendorAsset), (int)EAssetType.NPC },

            // others:
            { typeof(AirdropAsset), (int)EAssetType.NONE },
            { typeof(CraftingAsset), (int)EAssetType.NONE },
            { typeof(CraftingBlacklistAsset), (int)EAssetType.NONE },
            { typeof(FoliageInfoAsset), (int)EAssetType.NONE },
            { typeof(FoliageInfoCollectionAsset), (int)EAssetType.NONE },
            { typeof(ItemCurrencyAsset), (int)EAssetType.NONE },
            { typeof(LandscapeMaterialAsset), (int)EAssetType.NONE },
            { typeof(LevelAsset), (int)EAssetType.NONE },
            { typeof(MaterialPaletteAsset), (int)EAssetType.NONE },
            { typeof(NPCRewardsAsset), (int)EAssetType.NONE },
            { typeof(OutfitAsset), (int)EAssetType.NONE },
            { typeof(PhysicsMaterialAssetBase), (int)EAssetType.NONE },
            { typeof(RoadAsset), (int)EAssetType.NONE },
            { typeof(ServerListCurationAsset), (int)EAssetType.NONE },
            { typeof(StereoSongAsset), (int)EAssetType.NONE },
            { typeof(TagAsset), (int)EAssetType.NONE },
            { typeof(VehiclePhysicsProfileAsset), (int)EAssetType.NONE },
            { typeof(WeatherAssetBase), (int)EAssetType.NONE },
            { typeof(ZombieDifficultyAsset), (int)EAssetType.NONE }
        };
    }

    internal static StringComparer FileNameComparer { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static StringComparison FileNameComparison { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private enum NextValueType { None = -1, Metadata, Asset, Guid, Id, Type, AssetCategory }

    internal static int GetAssetType(Type assetType)
    {
        if (assetType is null || assetType == typeof(Asset))
        {
            return (int)EAssetType.NONE;
        }

        if (assetType.IsSubclassOf(typeof(RedirectorAsset)))
            throw new InvalidOperationException("Unable to determine redirector asset category.");

        lock (CachedAssetTypes)
        {
            if (CachedAssetTypes.TryGetValue(assetType, out int typeInt))
            {
                return typeInt;
            }

            foreach (KeyValuePair<Type, int> types in CachedAssetTypes)
            {
                if (!assetType.IsSubclassOf(types.Key))
                    continue;

                CachedAssetTypes.Add(assetType, types.Value);
                return types.Value;
            }

            if (!assetType.IsSubclassOf(typeof(Asset)))
            {
                return (int)EAssetType.NONE;
            }

            try
            {
                Asset a = (Asset)Activator.CreateInstance(assetType);
                typeInt = (int)a.assetCategory;
            }
            catch (Exception ex)
            {
                CommandWindow.LogError("Error creating asset for category check.");
                CommandWindow.LogError(ex);
            }

            CachedAssetTypes.Add(assetType, typeInt);
            return typeInt;
        }
    }

    internal static Guid ReadAssetIdentifierDetails(ref FastDatTokenizer tokenizer, out ushort id, out int category)
    {
        id = 0;
        category = (int)EAssetType.NONE;
        Guid guidRtn = Guid.Empty;

        string? typeStr = null;
        bool typeStrCanBeAlias = false;
        bool typeIsRedirect = false;

        int dictionaryDepth = 0, listDepth = 0;
        bool isInMetadata = false, isExplicitlyInAsset = false, ignoreExplicitAsset = false, hasAssets = false;
        bool hasMetadata = false, ignoreMetadata = false;
        ReadOnlySpan<char> metaKey = "Metadata".AsSpan();
        ReadOnlySpan<char> assetKey = "Asset".AsSpan();
        ReadOnlySpan<char> guidKey = "GUID".AsSpan();
        ReadOnlySpan<char> typeKey = "Type".AsSpan();
        ReadOnlySpan<char> idKey = "ID".AsSpan();
        ReadOnlySpan<char> assetCategoryKey = "AssetCategory".AsSpan();

        NextValueType nextValueType = NextValueType.None;

        bool hasGuid = false, hasId = false, hasType = false, hasTypeInMetadata = false, hasAssetCategory = false;
        
        EAssetType overrideAssetCategory = EAssetType.NONE;

        while (tokenizer.MoveNext())
        {
            if (hasGuid && hasId && hasType && (!typeIsRedirect || hasAssetCategory))
            {
                break;
            }

            switch (tokenizer.Token.Type)
            {
                case DatTokenType.Key:

                    ReadOnlySpan<char> content = tokenizer.Token.Content;
                    if (dictionaryDepth == 0 && listDepth == 0 && !ignoreMetadata && content.Equals(metaKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.Metadata;
                    }
                    else if (dictionaryDepth == 0 && listDepth == 0 && !ignoreExplicitAsset && content.Equals(assetKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.Asset;
                    }
                    else if (!hasType && (!hasMetadata && dictionaryDepth == 0 || isInMetadata && dictionaryDepth == 1)
                                      && (isInMetadata || !hasTypeInMetadata)
                                      && listDepth == 0
                                      && content.Equals(typeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.Type;
                    }
                    else if (!hasGuid && (!hasMetadata && dictionaryDepth == 0 || isInMetadata && dictionaryDepth == 1) && listDepth == 0 && content.Equals(guidKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.Guid;
                    }
                    else if (!hasId && dictionaryDepth == (isExplicitlyInAsset ? 1 : 0) && listDepth == 0 && content.Equals(idKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.Id;
                    }
                    else if (!hasAssetCategory && dictionaryDepth == (isExplicitlyInAsset ? 1 : 0) && listDepth == 0 && content.Equals(assetCategoryKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextValueType = NextValueType.AssetCategory;
                    }
                    else
                    {
                        nextValueType = NextValueType.None;
                    }

                    break;

                case DatTokenType.Value:
                    content = tokenizer.Token.Content;
                    switch (nextValueType)
                    {
                        case NextValueType.Metadata:
                            isInMetadata = false;
                            hasMetadata = false;
                            ignoreMetadata = true;
                            break;

                        case NextValueType.Asset:
                            isExplicitlyInAsset = false;
                            hasAssets = false;
                            ignoreExplicitAsset = true;
                            break;

                        case NextValueType.Guid:
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                            if (Guid.TryParse(content, out Guid guid))
#else
                            if (Guid.TryParse(content.ToString(), out Guid guid))
#endif
                            {
                                guidRtn = guid;
                            }

                            hasGuid = true;
                            break;

                        case NextValueType.Id:
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                            if (ushort.TryParse(content, NumberStyles.Number, CultureInfo.InvariantCulture, out ushort parsedId))
#else
                            if (ushort.TryParse(content.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out ushort parsedId))
#endif
                            {
                                id = parsedId;
                            }
                            else
                            {
                                id = 0;
                            }

                            hasId = true;
                            break;

                        case NextValueType.Type:
                            typeIsRedirect = content.StartsWith("SDG.Unturned.RedirectorAsset", StringComparison.OrdinalIgnoreCase)
                                             || !isInMetadata && content.Equals("Redirector", StringComparison.OrdinalIgnoreCase);
                            hasType = true;
                            hasTypeInMetadata |= isInMetadata;
                            typeStrCanBeAlias = !isInMetadata;
                            if (!typeIsRedirect)
                                typeStr = content.ToString();
                            break;

                        case NextValueType.AssetCategory:
                            if (typeIsRedirect && Enum.TryParse(content.ToString(), true, out EAssetType assetType))
                            {
                                overrideAssetCategory = assetType;
                            }

                            hasAssetCategory = true;
                            break;
                    }

                    nextValueType = NextValueType.None;
                    break;

                case DatTokenType.ListValue:
                    nextValueType = NextValueType.None;
                    break;

                case DatTokenType.DictionaryStart:
                    ++dictionaryDepth;
                    if (dictionaryDepth != 1 || listDepth != 0)
                    {
                        nextValueType = NextValueType.None;
                        break;
                    }

                    switch (nextValueType)
                    {
                        case NextValueType.Metadata when !hasMetadata:
                            isInMetadata = true;
                            hasMetadata = true;
                            hasType = false;
                            hasGuid = false;
                            break;

                        case NextValueType.Asset when !hasAssets:
                            isExplicitlyInAsset = true;
                            hasAssets = true;
                            hasId = false;
                            break;

                        case NextValueType.Guid:
                            hasGuid = true;
                            break;

                        case NextValueType.Id:
                            hasId = true;
                            break;

                        case NextValueType.Type:
                            hasType = true;
                            break;

                        case NextValueType.AssetCategory:
                            hasAssetCategory = true;
                            break;
                    }

                    nextValueType = NextValueType.None;
                    break;

                case DatTokenType.DictionaryEnd:
                    if (dictionaryDepth == 1)
                    {
                        isInMetadata = false;
                        isExplicitlyInAsset = false;
                    }

                    nextValueType = NextValueType.None;
                    --dictionaryDepth;
                    break;

                case DatTokenType.ListStart:
                    ++listDepth;

                    if (dictionaryDepth != 0 || listDepth != 1)
                    {
                        nextValueType = NextValueType.None;
                        break;
                    }

                    switch (nextValueType)
                    {
                        case NextValueType.Metadata when !hasMetadata:
                            hasMetadata = true;
                            ignoreMetadata = true;
                            break;

                        case NextValueType.Asset:
                            hasAssets = true;
                            ignoreExplicitAsset = true;
                            break;

                        case NextValueType.Guid:
                            hasGuid = true;
                            break;

                        case NextValueType.Id:
                            hasId = true;
                            break;

                        case NextValueType.Type:
                            hasType = true;
                            break;

                        case NextValueType.AssetCategory:
                            hasAssetCategory = true;
                            break;
                    }

                    nextValueType = NextValueType.None;
                    break;

                case DatTokenType.ListEnd:
                    --listDepth;
                    nextValueType = NextValueType.None;
                    break;

            }

            if (isInMetadata && dictionaryDepth <= 0)
            {
                isInMetadata = false;
            }
            if (isExplicitlyInAsset && dictionaryDepth <= 0)
            {
                isExplicitlyInAsset = false;
            }
        }

        if (hasId)
        {
            if (typeIsRedirect)
            {
                category = (int)overrideAssetCategory;
            }
            else if (hasType)
            {
                Type? type = null;
                if (typeStrCanBeAlias)
                {
                    type = Assets.assetTypes.getType(typeStr);
                }

                type ??= Type.GetType(typeStr!, throwOnError: false, ignoreCase: true)
                         ?? typeof(UnturnedNexus).Assembly.GetType(typeStr!, throwOnError: false, ignoreCase: true);

                category = type == typeof(RedirectorAsset) ? (int)overrideAssetCategory : GetAssetType(type);
            }
            else
            {
                category = (int)EAssetType.NONE;
            }
        }

        return guidRtn;
    }
}
