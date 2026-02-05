using System;
using OpenMod.API.Plugins;

namespace uTest.Compat.OpenMod.DependencyInjection;

/// <summary>
/// Associates a plugin with this assembly, fixture, or test, allowing injecting plugin-scope services.
/// </summary>
/// <param name="pluginType">A type from the plugin assembly, usually the <see cref="IOpenModPlugin"/> type.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class AssociatedPluginAttribute(Type? pluginType) : Attribute
{
    /// <summary>
    /// A type from the plugin assembly, usually the <see cref="IOpenModPlugin"/> type.
    /// </summary>
    public Type? PluginType { get; } = pluginType;
}