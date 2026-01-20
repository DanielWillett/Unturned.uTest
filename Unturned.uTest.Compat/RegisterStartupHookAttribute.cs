using System;

namespace uTest.Compat;

/// <summary>
/// Defines an implementation of <see cref="IStartupHook"/> to use when this assembly is loaded.
/// </summary>
/// <param name="type">
/// The type of startup hook, which should implement <see cref="IStartupHook"/> and define a parameterless constructor.
/// It can optionally implement <see cref="IDisposable"/> which will be called after startup.
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RegisterStartupHookAttribute(Type type) : Attribute
{
    /// <summary>
    /// The type of startup hook, which should implement <see cref="IStartupHook"/> and define a parameterless constructor.
    /// It can optionally implement <see cref="IDisposable"/> which will be called after startup.
    /// </summary>
    public Type Type { get; } = type;
}