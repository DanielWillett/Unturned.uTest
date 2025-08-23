using System.ComponentModel;

namespace uTest.Runner;

/// <summary>
/// Attribute used by the source-generator to designate <see cref="IGeneratedTestProvider"/> implementations for this assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedTestBuilderAttribute : Attribute
{
    /// <summary>
    /// Type of <see cref="IGeneratedTestProvider"/> with a public parameterless constructor.
    /// </summary>
    public Type Type { get; }


    /// <summary>
    /// The type where tests were originally defined in.
    /// </summary>
    public Type TestType { get; }

    public GeneratedTestBuilderAttribute(Type testType, Type type)
    {
        TestType = testType;
        Type = type;
    }
}