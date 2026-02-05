// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

internal sealed class IsExternalInit;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public const string RefStructs = "RefStructs";
    public const string RequiredMembers = "RequiredMembers";
    public string FeatureName { get; } = featureName;
    public bool IsOptional { get; init; }
}