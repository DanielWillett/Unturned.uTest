using JetBrains.Annotations;

namespace uTest.Runner;

/// <summary>
/// Interface used by the source-generator to list all tests in this assembly.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
[UsedImplicitly]
public interface IGeneratedTestProvider
{
    void Build(IGeneratedTestBuilder builder);
}