using System;

namespace uTest;

/// <summary>
/// Defines a set of values for a parameter, all of which need to be tested.
/// When used with other parameters, all possible combinations will be tested. 
/// </summary>
/// <remarks>Can also be used on generic parameters, but not with <see cref="From"/>.</remarks>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.GenericParameter)]
public sealed class SetAttribute : Attribute
{
    ///// <summary>
    ///// List of values for this parameter that require testing.
    ///// </summary>
    ///// <remarks>All values should be the same type or convertible to the parameter type.</remarks>
    //public object?[]? Values { get; }

    /// <summary>
    /// The name of a member (field, property, method) to fetch the arguments from in the declaring type.
    /// This is mainly useful for types that aren't supported by attributes.
    /// <para>
    /// The member must be a static array of the parameter type.
    /// </para>
    /// <example>
    /// <code>
    /// // Example
    /// private static readonly DateTime[] TimeSet =
    /// [
    ///     new DateTime(2020, 03, 15, 12, 0, 0),
    ///     new DateTime(1970, 1,  1,  0,  0, 0)
    /// ];
    /// 
    /// [Test]
    /// public void CheckDate([Set(From = nameof(TimeSet))] DateTime times)
    /// {
    ///     // not implemented
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public required string? From { get; set; }

    /// <summary>
    /// Defines a set of values for a parameter declared by a member in this type using the <see cref="From"/> property,
    /// all of which need to be tested. When used with other parameters, all possible combinations will be tested. 
    /// </summary>
    /// <remarks>
    /// This constructor should NOT be used with generic parameters.
    /// Only <see cref="SetAttribute(object[])"/> (with <see cref="Type"/> values) should be used for generic parameters.
    /// </remarks>
    public SetAttribute() { }

    /// <summary>
    /// Defines a set of values for a parameter, all of which need to be tested.
    /// When used with other parameters, all possible combinations will be tested. 
    /// <example>
    /// <code>
    /// // Example
    /// [Test]
    /// public void CheckSomeCalculation(
    ///     [Set(1, 3, 9, 27, 81)] int powerOf3,
    ///     [Set(true, false)]     bool useFloat32
    /// )
    /// {
    ///     // not implemented
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="values">
    /// List of parameter values in the order they're declared by the method definition.
    /// <para>All values should be the same type or convertible to the parameter type.</para>
    /// </param>
    /// <remarks>
    /// This constructor can be used with <see cref="Type"/> values on generic parameters.
    /// </remarks>
    [SetsRequiredMembers]
    public SetAttribute(params object?[]? values)
    {
        //Values = values;
        //From = null;
    }
}