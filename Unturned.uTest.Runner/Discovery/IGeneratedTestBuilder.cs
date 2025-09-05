namespace uTest.Runner;

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

    public GeneratedTestBuilder(List<UnturnedTest> tests)
    {
        Tests = tests;
    }

    /// <inheritdoc />
    public int MethodCount
    {
        set => Tests.Capacity = Tests.Count + value;
    }

    /// <inheritdoc />
    public Type? TestType { get; set; }

    /// <inheritdoc />
    public UnturnedTestParameter[]? TypeParameters { get; set; }
    public UnturnedTestArgs[]? TypeArgs { get; set; }

    public void Add(UnturnedTest test)
    {
        if (test == null)
            throw new ArgumentNullException(nameof(test));

        Tests.Add(test);
    }

    /// <inheritdoc />
    public override string ToString() => Tests.Count + " test(s)";
}