using System;

namespace uTest;

/// <summary>
/// Defines one or multiple assets that this test needs to have loaded to run using a <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> in a mod or folder.
/// </summary>
/// <remarks>Other assets that will be loaded include assets loaded by the level, already placed assets, and some select core assets.</remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class RequiredAssetsAttribute : Attribute, IRequiredAssetContributorAttribute
{
    internal const Source SourceMap = (Source)(-1);
    internal const Source SourceMod = (Source)(-2);

    /// <summary>
    /// The <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> to use for matching asset files.
    /// </summary>
    public string GlobPattern { get; }

    /// <summary>
    /// The name of the mod or map mod who's <c>Bundles</c> folder will be used as the root folder for this attribute.
    /// </summary>
    /// <remarks>The mod must already be installed.</remarks>
    public ulong ModId { get; }

    /// <summary>
    /// The name of the map in the <c>Maps</c> folder who's <c>Bundles</c> folder will be used as the root folder for this attribute.
    /// </summary>
    public string? MapName { get; }

    /// <summary>
    /// The type of root folder to use for this attribute.
    /// </summary>
    /// <remarks>May have undefined enum values for maps and mods (-1 and -2).</remarks>
    public Source RootFolderSource { get; }

    /// <inheritdoc cref="RequiredAssetsAttribute"/>
    /// <param name="globPattern">
    /// A case-sensitive <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> with the root being
    /// the <c>Bundles</c> folder in the game's installation folder.
    /// <para>
    /// Example: <c>Assets/Landscapes/*Gun*/**/*Washington*/**</c> will select all landscape materials or foliage containing 'Washington'.
    /// </para>
    /// The selector needs to select the <c>.dat</c> or <c>.asset</c> file the asset was read from.
    /// </param>
    public RequiredAssetsAttribute(string globPattern) : this(Source.Core, globPattern) { }

    /// <inheritdoc cref="RequiredAssetsAttribute"/>
    /// <param name="rootFolderSource">The root folder for the glob pattern to match against.</param>
    /// <param name="globPattern">
    /// A case-sensitive <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> with the root being
    /// either the <c>Bundles</c> or <c>Sandbox</c> folder in the game's installation folder, depending on the value of <paramref name="rootFolderSource"/>.
    /// <para>
    /// Example (Source.Core): <c>Assets/Landscapes/*Gun*/**/*Washington*/**</c> will select all landscape materials or foliage containing 'Washington'.
    /// </para>
    /// The selector needs to select the <c>.dat</c> or <c>.asset</c> file the asset was read from.
    /// </param>
    public RequiredAssetsAttribute(Source rootFolderSource, string globPattern)
    {
        if (rootFolderSource != Source.Sandbox)
            rootFolderSource = Source.Core;

        GlobPattern = globPattern;
        RootFolderSource = rootFolderSource;
    }

    /// <inheritdoc cref="RequiredAssetsAttribute"/>
    /// <param name="modId">The full ID of the workshop item to load assets from. The mod must already be installed. Legacy-defined server mods are not supported, they must use the WorkshopDownloadConfig.json file.</param>
    /// <param name="globPattern">
    /// A case-sensitive <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> with the root being
    /// either the mod's root folder or the <c>Bundles</c> folder if the mod is a map.
    /// <para>
    /// Example: <c>Items/**</c> will select all assets in the <c>Items</c> folder in a mod.
    /// </para>
    /// The selector needs to select the <c>.dat</c> or <c>.asset</c> file the asset was read from.
    /// </param>
    public RequiredAssetsAttribute(ulong modId, string globPattern)
    {
        GlobPattern = globPattern;
        ModId = modId;
        RootFolderSource = SourceMod;
    }

    /// <inheritdoc cref="RequiredAssetsAttribute"/>
    /// <param name="mapName">The name of a map in the <c>Maps</c> folder in the game's installation folder. This is case-sensitive.</param>
    /// <param name="globPattern">
    /// A case-sensitive <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing">file glob pattern</see> with the root being
    /// either the map's <c>Bundles</c> folder.
    /// <para>
    /// Example: <c>NPCs/**</c> will select all assets in the <c>Bundles/NPCs</c> folder in a mod.
    /// </para>
    /// The selector needs to select the <c>.dat</c> or <c>.asset</c> file the asset was read from.
    /// </param>
    public RequiredAssetsAttribute(string mapName, string globPattern)
    {
        GlobPattern = globPattern;
        MapName = mapName;
        RootFolderSource = SourceMap;
    }

    /// <summary>
    /// The source folder for a glob pattern's root folder.
    /// </summary>
    public enum Source
    {
        /// <summary>
        /// The <c>Bundles</c> folder in the game's install folder.
        /// </summary>
        Core,

        /// <summary>
        /// The <c>Sandbox</c> folder in the game's install folder.
        /// </summary>
        Sandbox
    }
}