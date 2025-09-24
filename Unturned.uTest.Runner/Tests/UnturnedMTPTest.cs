using Microsoft.Testing.Platform.Extensions.Messages;
using uTest.Discovery;

namespace uTest.Runner;

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public class UnturnedMTPTest : UnturnedTest
{
    public TestMethodIdentifierProperty? IdentifierInfo { get; init; }
    public TestFileLocationProperty? LocationInfo { get; init; }
}
