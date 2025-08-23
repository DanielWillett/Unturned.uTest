using System;
using JetBrains.Annotations;

namespace uTest;

/// <summary>
/// Defines a new test.
/// </summary>
[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
[BaseTypeRequired(typeof(ITestClass))]
public sealed class TestAttribute : Attribute
{
    /// <summary>
    /// A description/comment of the test.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Defines a new test.
    /// </summary>
    public TestAttribute() { }

    /// <summary>
    /// Defines a new test with a description.
    /// </summary>
    public TestAttribute(string description)
    {
        Description = description;
    }
}