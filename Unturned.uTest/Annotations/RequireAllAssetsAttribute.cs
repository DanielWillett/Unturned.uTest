using System;

namespace uTest;

/// <summary>
/// Indicates that the game should load all assets like it typically would.
/// </summary>
/// <remarks>This overrides any other <see cref="RequiredAssetAttribute"/> or <see cref="RequiredAssetsAttribute"/> annotations.</remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module)]
public sealed class RequireAllAssetsAttribute : Attribute, IRequiredAssetContributorAttribute;

internal interface IRequiredAssetContributorAttribute;