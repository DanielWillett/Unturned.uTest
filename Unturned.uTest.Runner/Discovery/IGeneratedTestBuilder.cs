namespace uTest.Runner;

/// <summary>
/// Sink interface for <see cref="IGeneratedTestProvider.Build"/> used by the source-generator to list all tests in this assembly.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public interface IGeneratedTestBuilder
{
    int MethodCount { set; }

    Type TestType { set; }

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

    public void Add(UnturnedTest test)
    {
        if (test == null)
            throw new ArgumentNullException(nameof(test));

        Tests.Add(test);
    }

    /// <inheritdoc />
    public override string ToString() => Tests.Count + " test(s)";
}