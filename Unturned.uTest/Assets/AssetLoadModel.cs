using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.IO;
using System.Reflection;
using uTest.Discovery;
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

    internal AssetLoadModel(HashSet<Guid> guidsToLoad, HashSet<IdEntry> idsToLoad, FileGlobPattern[] fileGlobs) : this()
    {
        _requiresAll = false; // overrides other ctor, dont remove
        _guidsToLoad = guidsToLoad;
        _fileGlobs = fileGlobs;
        _idsToLoad = idsToLoad;
    }

    public bool Includes(string assetFile)
    {
        if (_requiresAll)
            return true;

        Assets.shouldLoadAnyAssets.value = false;

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
                            FileGlobPattern p = new FileGlobPattern(matcher, assetPattern.MapName, assetPattern.ModId, assetPattern.RootFolderSource);
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
        public readonly ulong ModId;
        public readonly RequiredAssetsAttribute.Source Source;

        public FileGlobPattern(Matcher matcher, string? mapName, ulong modId, RequiredAssetsAttribute.Source source)
        {
            Matcher = matcher;
            MapName = mapName;
            ModId = modId;
            Source = source;
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
