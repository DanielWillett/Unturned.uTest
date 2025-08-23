using System.Reflection;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace uTest.Runner;

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public class UnturnedTest
{
    public required string ManagedType { get; init; }
    public required string ManagedMethod { get; init; }
    public required string DisplayName { get; init; }
    public required string Uid { get; init; }
    public required MethodInfo Method { get; init; }
    public string? ParentUid { get; init; }

    public TestMethodIdentifierProperty? IdentifierInfo { get; init; }
    public TestFileLocationProperty? LocationInfo { get; init; }


    /// <inheritdoc />
    public override string ToString() => Uid;

    internal void AddProperties(TestNode node)
    {
        if (LocationInfo != null)
            node.Properties.Add(LocationInfo);
        if (IdentifierInfo != null)
            node.Properties.Add(IdentifierInfo);
    }
}
