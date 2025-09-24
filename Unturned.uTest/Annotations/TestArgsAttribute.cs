using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// Defines a set of arguments to use for one test. A test can declare this attribute multiple times. 
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed class TestArgsAttribute : Attribute
{
    ///// <summary>
    ///// List of parameter values in the order they're declared by the method definition.
    ///// </summary>
    //public object?[]? Args { get; }

    /// <summary>
    /// The name of a member (field, property, method) to fetch the arguments from in the declaring type.
    /// This is mainly useful for types that aren't supported by attributes.
    /// <para>
    /// The member should be a static or instance array of an anonymous type,
    /// where the names of each property are an exact match for the parameter names (case-sensitive).
    /// </para>
    /// <para>
    /// Note that while multiple of these arguments are supported, it usually only makes sense to have one when using this property.
    /// </para>
    /// <example>
    /// <code>
    /// // Example
    /// private static readonly Array ParseNumberArgs = new[]
    /// {
    ///     new { input = "123",  expected = 123  },
    ///     new { input = "0",    expected = 0    },
    ///     new { input = "-123", expected = -123 },
    /// };
    ///
    /// [Test]
    /// [TestArgs(From = nameof(ParseNumberArgs))]
    /// public void ParseNumber(string input, int expected)
    /// {
    ///     // not implemented
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public required string? From { get; set; }

    /// <summary>
    /// Defines a set of arguments declared by a member in this type using the <see cref="From"/> property.
    /// </summary>
    public TestArgsAttribute() { }

    /// <summary>
    /// Defines a set of arguments to use for one test. A test can declare this attribute multiple times. 
    /// <example>
    /// <code>
    /// // Example
    /// [Test]
    /// [TestArgs("123", 123)]
    /// [TestArgs("0", 0)]
    /// [TestArgs("-123", -123)]
    /// public void ParseNumber(string input, int expected)
    /// {
    ///     // not implemented
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="args">List of parameter values in the order they're declared by the method definition.</param>
    [SetsRequiredMembers]
    public TestArgsAttribute(params object?[]? args)
    {
        //Args = args;
        From = null;
    }
}