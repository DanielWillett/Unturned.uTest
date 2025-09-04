using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// Defines a set of type arguments to use for one test or class. A test/class can declare this attribute multiple times.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed class TypeArgsAttribute : Attribute
{
    ///// <summary>
    ///// List of types that correspond to the generic type parameters for this class or method.
    ///// </summary>
    //public Type[]? Types { get; }

    /// <summary>
    /// Defines a set of type arguments to use for one test or class. A test/class can declare this attribute multiple times.
    /// <example>
    /// <code>
    /// // Example
    /// [Test]
    /// [TypeArgs(typeof(int), typeof(ConsoleColor))]
    /// [TypeArgs(typeof(long), typeof(BindingFlags))]
    /// public void ConvertNumberToEnum&lt;TNumber, TEnum&gt;()
    /// {
    ///     // not implemented
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="types">List of types that correspond to the generic type parameters for this class or method.</param>
    public TypeArgsAttribute(params Type[] types)
    {
        //Types = types;
    }
}