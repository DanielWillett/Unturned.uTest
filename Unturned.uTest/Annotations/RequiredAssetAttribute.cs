using System;

namespace uTest;

/// <summary>
/// Defines an asset that this test needs to have loaded to run.
/// </summary>
/// <remarks>Other assets that will be loaded include assets loaded by the level, already placed assets, and some select core assets.</remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class RequiredAssetAttribute(string guid) : Attribute, IRequiredAssetContributorAttribute
{
    public string? GuidStr { get; } = guid;

    internal ushort Id { get; }

    internal int Type { get; }

    /// <summary>
    /// Defines an asset that this test needs to have loaded to run by it's ID and asset type.
    /// </summary>
    public RequiredAssetAttribute(ushort id, EAssetType assetType) : this(null!)
    {
        Id = id;
        Type = (int)assetType;
    }
}