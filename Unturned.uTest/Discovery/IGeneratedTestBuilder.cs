using System;

namespace uTest.Discovery;

/// <summary>
/// Sink interface for <see cref="IGeneratedTestProvider.Build"/> used by the source-generator to list all tests in this assembly.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public interface IGeneratedTestBuilder
{
    /// <summary>
    /// Number of methods that will be added. Should be set first.
    /// </summary>
    int MethodCount { set; }

    /// <summary>
    /// The base test type.
    /// </summary>
    Type TestType { set; }

    /// <summary>
    /// List of type parameters.
    /// </summary>
    UnturnedTestParameter[]? TypeParameters { set; }
    
    /// <summary>
    /// List of valid <see cref="TypeArgsAttribute"/> attributes.
    /// </summary>
    public UnturnedTestArgs[]? TypeArgs { set; }

    /// <summary>
    /// Adds a new test.
    /// </summary>
    void Add(UnturnedTest test);
}

internal class GeneratedTestBuilder : IGeneratedTestBuilder
{
    internal List<UnturnedTest> Tests;

    internal UnturnedTestOwnerInfo OwnerInfo;

    public GeneratedTestBuilder(List<UnturnedTest> tests, Type type)
    {
        Tests = tests;
        OwnerInfo = new UnturnedTestOwnerInfo(type);
    }

    /// <inheritdoc />
    public int MethodCount
    {
        set => Tests.Capacity = Tests.Count + value;
    }

    /// <inheritdoc />
    public Type? TestType
    {
        get => OwnerInfo.Type;
        set
        {
            if (value != null)
                OwnerInfo.Type = value;
        }
    }

    /// <inheritdoc />
    public UnturnedTestParameter[]? TypeParameters
    {
        get => OwnerInfo.TypeParameters;
        set => OwnerInfo.TypeParameters = value;
    }

    public UnturnedTestArgs[]? TypeArgs
    {
        get => OwnerInfo.TypeArgs;
        set => OwnerInfo.TypeArgs = value;
    }

    public void Add(UnturnedTest test)
    {
        if (test == null)
            throw new ArgumentNullException(nameof(test));

        test.Owner = OwnerInfo;
        Tests.Add(test);
    }

    /// <inheritdoc />
    public override string ToString() => Tests.Count + " test(s)";
}