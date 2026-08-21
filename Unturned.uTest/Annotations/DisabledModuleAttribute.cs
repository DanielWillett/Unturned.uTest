using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// Starts up the server with the given module disabled.
/// </summary>
/// <param name="moduleName">
/// The <c>"Name"</c> of the module to disable.
/// </param>
/// <remarks>Note this does not work when launching via the <c>U3-SDK</c>.</remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public sealed class DisabledModuleAttribute(string moduleName) : Attribute
{
    /// <summary>
    /// The <c>"Name"</c> of the module to disable.
    /// </summary>
    public string ModuleName { get; } = moduleName;
}