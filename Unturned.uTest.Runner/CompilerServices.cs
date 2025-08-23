namespace System.Runtime.CompilerServices;

sealed class IsExternalInit;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public const string RefStructs = "RefStructs";
    public const string RequiredMembers = "RequiredMembers";
    public string FeatureName { get; } = featureName;
    public bool IsOptional { get; init; }
}