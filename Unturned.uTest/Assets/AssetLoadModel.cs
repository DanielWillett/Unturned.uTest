using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using uTest.Module;

namespace uTest;

internal class AssetLoadModel
{
    internal record struct IdEntry(ushort Id, EAssetType Type);

    private readonly HashSet<Guid>? _guidsToLoad;
    private readonly HashSet<IdEntry>? _idsToLoad;
    private readonly FileGlobPattern[]? _fileGlobs;
    private readonly bool _requiresAll;

    private readonly string _corePath;
    private readonly string _sandboxPath;
    private readonly string _mapsPath;

    internal AssetLoadModel()
    {
        _requiresAll = true;
        _corePath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Bundles");
        _sandboxPath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Sandbox");
        _mapsPath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Maps");
    }

    internal AssetLoadModel(HashSet<Guid>? guidsToLoad, HashSet<IdEntry>? idsToLoad, FileGlobPattern[]? fileGlobs) : this()
    {
        _requiresAll = false; // overrides other ctor, dont remove
        _guidsToLoad = guidsToLoad;
        _fileGlobs = fileGlobs;
        _idsToLoad = idsToLoad;
    }

    internal void WriteToJson(JsonWriter json)
    {
        json.WriteStartObject();
        if (_requiresAll)
        {
            json.WritePropertyName("all");
            json.WriteValue(true);
        }
        else
        {
            if (_guidsToLoad != null)
            {
                json.WritePropertyName("guids");
                json.WriteStartArray();
                foreach (Guid guid in _guidsToLoad)
                {
                    json.WriteValue(guid);
                }
                json.WriteEndArray();
            }

            if (_idsToLoad != null)
            {
                json.WritePropertyName("ids");
                json.WriteStartArray();
                foreach (IdEntry id in _idsToLoad)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("id");
                    json.WriteValue(id.Id);
                    json.WritePropertyName("type");
                    json.WriteValue((int)id.Type);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }

            if (_fileGlobs != null)
            {
                json.WritePropertyName("files");
                json.WriteStartArray();
                foreach (FileGlobPattern pattern in _fileGlobs)
                {
                    json.WriteStartObject();

                    json.WritePropertyName("match");
                    json.WriteValue(pattern.OriginalGlob);

                    switch (pattern.Source)
                    {
                        case RequiredAssetsAttribute.SourceMod:
                            json.WritePropertyName("mod-id");
                            json.WriteValue(pattern.ModId);
                            break;

                        case RequiredAssetsAttribute.SourceMap:
                            json.WritePropertyName("map-name");
                            json.WriteValue(pattern.MapName);
                            break;

                        default:
                            json.WritePropertyName("source");
                            json.WriteValue((int)pattern.Source);
                            break;
                    }

                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
        }

        json.WriteEndObject();
    }

    internal static bool TryReadFromJson(JsonReader jsonReader, [NotNullWhen(true)] out AssetLoadModel? model)
    {
        model = null;

        bool isAll = false;
        HashSet<Guid>? guids = null;
        HashSet<IdEntry>? ids = null;
        List<FileGlobPattern>? globs = null;
        if (jsonReader.TokenType == JsonToken.None && !jsonReader.Read())
            return false;
        while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndObject)
        {
            if (jsonReader.TokenType != JsonToken.PropertyName)
                return false;

            switch ((string?)jsonReader.Value)
            {
                case "all":
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.Boolean)
                        return false;

                    isAll = (bool)jsonReader.Value;
                    break;

                case "guids":
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.StartArray)
                        return false;

                    guids = new HashSet<Guid>();
                    while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                    {
                        if (jsonReader.TokenType != JsonToken.String || !Guid.TryParse((string)jsonReader.Value, out Guid guid))
                            return false;

                        guids.Add(guid);
                    }
                    break;

                case "ids":
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.StartArray)
                        return false;

                    ids = new HashSet<IdEntry>();
                    while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                    {
                        if (jsonReader.TokenType != JsonToken.StartObject)
                            return false;

                        ushort id = 0;
                        EAssetType type = EAssetType.NONE;
                        while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndObject)
                        {
                            if (jsonReader.TokenType != JsonToken.PropertyName)
                                return false;

                            switch ((string?)jsonReader.Value)
                            {
                                case "id":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.Integer)
                                        return false;

                                    id = Convert.ToUInt16(jsonReader.Value);
                                    break;

                                case "type":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.Integer)
                                        return false;

                                    type = (EAssetType)Convert.ToInt32(jsonReader.Value);
                                    break;
                            }
                        }

                        if (id == 0 || type is <= EAssetType.NONE or > EAssetType.NPC)
                            return false;

                        ids.Add(new IdEntry(id, type));
                    }
                    break;

                case "files":
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.StartArray)
                        return false;

                    globs = new List<FileGlobPattern>();
                    while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                    {
                        if (jsonReader.TokenType != JsonToken.StartObject)
                            return false;

                        string? match = null, mapName = null;
                        ulong modId = 0;
                        RequiredAssetsAttribute.Source src = RequiredAssetsAttribute.Source.Core;
                        while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndObject)
                        {
                            if (jsonReader.TokenType != JsonToken.PropertyName)
                                return false;

                            switch ((string?)jsonReader.Value)
                            {
                                case "match":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.String)
                                        return false;

                                    match = (string)jsonReader.Value;
                                    break;
                                    
                                case "mod-id":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.Integer)
                                        return false;

                                    modId = Convert.ToUInt64(jsonReader.Value);
                                    src = RequiredAssetsAttribute.SourceMod;
                                    break;
                                    
                                case "map-name":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.String)
                                        return false;

                                    mapName = (string)jsonReader.Value;
                                    src = RequiredAssetsAttribute.SourceMap;
                                    break;
                                    
                                case "source":
                                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.Integer)
                                        return false;

                                    src = (RequiredAssetsAttribute.Source)Convert.ToInt32(jsonReader.Value);
                                    break;
                            }
                        }

                        if (src is < RequiredAssetsAttribute.SourceMod or > RequiredAssetsAttribute.Source.Sandbox
                            || src == RequiredAssetsAttribute.SourceMod && modId == 0
                            || src == RequiredAssetsAttribute.SourceMap && string.IsNullOrEmpty(mapName)
                            || string.IsNullOrEmpty(match))
                        {
                            return false;
                        }

                        Matcher matcher = new Matcher(StringComparison.Ordinal);
                        string glob = match;
                        bool exclude = glob[0] == '-';
                        if (exclude)
                        {
                            if (glob.Length == 1)
                                continue;
                            glob = glob[1..];
                        }

                        if (exclude)
                            matcher.AddExclude(glob);
                        else
                            matcher.AddInclude(glob);
                        globs.Add(new FileGlobPattern(match, matcher, mapName, modId, src));
                    }
                    break;
            }
        }

        model = isAll ? new AssetLoadModel() : new AssetLoadModel(guids, ids, globs?.ToArray());
        return true;
    }

    public bool Includes(string assetFile)
    {
        if (_requiresAll)
            return true;

        if (_fileGlobs != null)
        {
            if (assetFile.StartsWith(_corePath))
            {
                if (IsGlobMatch(RequiredAssetsAttribute.Source.Core, _corePath, assetFile))
                    return true;
            }
            else if (assetFile.StartsWith(_sandboxPath))
            {
                if (IsGlobMatch(RequiredAssetsAttribute.Source.Sandbox, _corePath, assetFile))
                    return true;
            }
            else if (assetFile.StartsWith(_mapsPath))
            {
                int slashIndex = assetFile.IndexOf(Path.DirectorySeparatorChar, _mapsPath!.Length + 1);
                if (slashIndex >= 0)
                {
                    string mapName = assetFile.Substring(_mapsPath.Length + 1, slashIndex - _mapsPath.Length - 1);
                    if (IsGlobMatch(RequiredAssetsAttribute.SourceMap, Path.Combine(_mapsPath, mapName, "Bundles"), assetFile))
                        return true;
                }
            }
            else
            {
                ulong modId = 0;
                string? rootModPath = null;
                List<SteamContent> ugcList = Dedicator.isStandaloneDedicatedServer ? DedicatedUGC.ugc : Provider.provider.workshopService.ugc;
                foreach (SteamContent ugc in ugcList)
                {
                    if (!assetFile.StartsWith(ugc.path))
                        continue;

                    modId = ugc.publishedFileID.m_PublishedFileId;
                    rootModPath = ugc.path;
                    if (ugc.type == ESteamUGCType.MAP)
                        rootModPath = Path.Combine(rootModPath, "Bundles");
                    break;
                }

                if (modId != 0)
                {
                    if (IsGlobMatch(RequiredAssetsAttribute.SourceMod, Path.Combine(_mapsPath, rootModPath, "Bundles"), assetFile))
                        return true;
                }
            }
        }

        if (_guidsToLoad != null || _idsToLoad != null)
        {
            string fileText = File.ReadAllText(assetFile);
            ReadOnlySpan<char> fileSpan = fileText;

            FastDatTokenizer tokenizer = new FastDatTokenizer(fileSpan);
            Guid guid = FileHelper.ReadAssetIdentifierDetails(ref tokenizer, out ushort id, out int categoryInt);
            EAssetType category = (EAssetType)categoryInt;

            if (guid != Guid.Empty && _guidsToLoad != null)
            {
                if (_guidsToLoad.Contains(guid))
                    return true;
            }

            if (id != 0 && category != EAssetType.NONE && _idsToLoad != null)
            {
                if (_idsToLoad.Contains(new IdEntry(id, category)))
                    return true;
            }
        }

        Assets.shouldLoadAnyAssets.value = false;

        return false;
    }

    private bool IsGlobMatch(RequiredAssetsAttribute.Source expectedSource, string rootPath, string assetFile, string? mapName = null, ulong modId = 0)
    {
        if (_fileGlobs == null)
            return false;

        for (int i = 0; i < _fileGlobs.Length; ++i)
        {
            FileGlobPattern p = _fileGlobs[i];
            if (p.Source != expectedSource)
                continue;

            switch (expectedSource)
            {
                case RequiredAssetsAttribute.SourceMod:
                    if (p.ModId != modId)
                        continue;
                    break;

                case RequiredAssetsAttribute.SourceMap:
                    if (!string.Equals(p.MapName, mapName, StringComparison.Ordinal))
                        continue;
                    break;
            }

            PatternMatchingResult result = p.Matcher.Match(rootPath, assetFile);
            if (result.HasMatches)
                return true;
        }

        return false;
    }

    public static AssetLoadModel Create(MainModule module, bool includeDefaultAssets)
    {
        List<FileGlobPattern> patterns = new List<FileGlobPattern>();
        HashSet<Guid> guids = new HashSet<Guid>();
        HashSet<IdEntry> ids = new HashSet<IdEntry>();
        bool hasAny = false;

        // note: may not be on main thread
        UnturnedTestInstanceData[] tests = module.Tests;

        List<IRequiredAssetContributorAttribute> tempAttributeList = new List<IRequiredAssetContributorAttribute>(16);

        for (int i = 0; i < tests.Length && !hasAny; ++i)
        {
            UnturnedTestInstanceData inst = tests[i];

            MethodInfo method = inst.Instance.Method;

            TestAttributeHelper<IRequiredAssetContributorAttribute>.GetAttributes(method, tempAttributeList, inherit: true);
            hasAny |= CheckAttributes(tempAttributeList, patterns, guids, ids);
            tempAttributeList.Clear();
        }

        if (includeDefaultAssets)
        {
            DefaultAssets.Add(guids);
        }

        return hasAny ? new AssetLoadModel() : new AssetLoadModel(guids, ids, patterns.ToArray());

        static bool CheckAttributes(List<IRequiredAssetContributorAttribute> attributes, List<FileGlobPattern> patterns, HashSet<Guid> guids, HashSet<IdEntry> ids)
        {
            foreach (IRequiredAssetContributorAttribute attribute in attributes)
            {
                switch (attribute)
                {
                    case RequiredAssetAttribute oneAsset:
                        if (Guid.TryParse(oneAsset.GuidStr, out Guid guid) && guid != Guid.Empty)
                        {
                            guids.Add(guid);
                            continue;
                        }

                        if (oneAsset.Id != 0 && (EAssetType)oneAsset.Type != EAssetType.NONE)
                        {
                            ids.Add(new IdEntry(oneAsset.Id, (EAssetType)oneAsset.Type));
                            continue;
                        }

                        break;

                    case RequiredAssetsAttribute assetPattern:

                        string glob = assetPattern.GlobPattern;
                        if (glob.Length == 0)
                            continue;

                        bool exclude = glob[0] == '-';
                        if (exclude)
                        {
                            if (glob.Length == 1)
                                continue;
                            glob = glob[1..];
                        }

                        bool found = false;
                        foreach (FileGlobPattern p in patterns)
                        {
                            if (!p.SourceEquals(assetPattern))
                                continue;

                            if (exclude)
                                p.Matcher.AddExclude(glob);
                            else
                                p.Matcher.AddInclude(glob);
                            found = true;
                            break;
                        }

                        if (!found)
                        {
                            Matcher matcher = new Matcher(StringComparison.Ordinal);
                            if (exclude)
                                matcher.AddExclude(glob);
                            else
                                matcher.AddInclude(glob);
                            FileGlobPattern p = new FileGlobPattern(assetPattern.GlobPattern, matcher, assetPattern.MapName, assetPattern.ModId, assetPattern.RootFolderSource);
                            patterns.Add(p);
                        }
                        break;

                    case RequireAllAssetsAttribute:
                        return true;
                }
            }

            return false;
        }
    }

    internal class FileGlobPattern
    {
        public readonly Matcher Matcher;
        public readonly string? MapName;
        public readonly string? OriginalGlob;
        public readonly ulong ModId;
        public readonly RequiredAssetsAttribute.Source Source;

        public FileGlobPattern(string glob, Matcher matcher, string? mapName, ulong modId, RequiredAssetsAttribute.Source source)
        {
            Matcher = matcher;
            MapName = mapName;
            ModId = modId;
            Source = source;
            OriginalGlob = glob;
        }

        public bool SourceEquals(RequiredAssetsAttribute attribute)
        {
            if (attribute.RootFolderSource != Source)
                return false;

            return Source switch
            {
                RequiredAssetsAttribute.SourceMod => attribute.ModId == ModId,
                RequiredAssetsAttribute.SourceMap => string.Equals(attribute.MapName, MapName, StringComparison.Ordinal),
                _ => true
            };
        }
    }
}
