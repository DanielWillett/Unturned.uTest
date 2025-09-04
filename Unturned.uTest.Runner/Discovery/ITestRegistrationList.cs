using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using IMTPLogger = Microsoft.Testing.Platform.Logging.ILogger;

namespace uTest.Runner;

internal interface ITestRegistrationList : ITestFrameworkCapability
{
    Task<List<UnturnedTest>> GetTestsAsync(IMTPLogger logger, CancellationToken token = default);

    Task<List<UnturnedTestInstance>> ExpandTestsAsync(IMTPLogger logger, List<UnturnedTest> originalTests, CancellationToken token = default);
}

internal readonly struct UnturnedTestInstance
{
    public UnturnedTest Test { get; }
    public UnturnedTestArgument[] TypeArguments { get; }
    public UnturnedTestArgument[] Arguments { get; }

    public string Uid { get; }
    public string DisplayName { get; }
    public int Index { get; }
    public int ArgHash { get; }
    public bool HasParameters => Arguments.Length > 0;

    public UnturnedTestInstance(UnturnedTest test, UnturnedTestArgument[] typeArguments, UnturnedTestArgument[] arguments, string uid, string displayName, int index, int argHash)
    {
        Test = test;
        Arguments = arguments;
        Uid = uid;
        DisplayName = displayName;
        Index = index;
        ArgHash = argHash;
        TypeArguments = typeArguments;
    }

    internal static int CalculateArgumentHash(UnturnedTestArgument[] typeArguments, UnturnedTestArgument[] arguments)
    {
        int hashCode = -2128831035;
        foreach (UnturnedTestArgument arg in typeArguments)
        {
            hashCode = unchecked( (hashCode ^ ((Type)arg.Value!).GetHashCode()) * 16777619 );
        }
        foreach (UnturnedTestArgument arg in arguments)
        {
            if (arg.Value != null)
                hashCode = unchecked ( (hashCode ^ arg.Value.GetHashCode()) * 16777619 );
            else
                hashCode = ~hashCode;
        }

        return hashCode;
    }

    public TestNode CreateTestNode(out TestNodeUid? parentUid)
    {
        TestNode node = new TestNode
        {
            DisplayName = DisplayName,
            Uid = new TestNodeUid(Uid)
        };

        if (Test.LocationInfo != null)
            node.Properties.Add(Test.LocationInfo);
        if (Test.IdentifierInfo != null)
            node.Properties.Add(Test.IdentifierInfo);

        parentUid = null;
        if (HasParameters)
        {
            parentUid = new TestNodeUid(Test.Uid);
        }

        return node;
    }

    public TestNode CreateTestNode()
    {
        return CreateTestNode(out _);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return ArgHash ^ Test.Uid.GetHashCode();
    }
}

internal readonly struct UnturnedTestArgument : IEquatable<UnturnedTestArgument>
{
    public static readonly UnturnedTestArgument Null = new UnturnedTestArgument(null);

    public object? Value { get; }

    public UnturnedTestArgument(Type type)
    {
        Value = type;
    }
    public UnturnedTestArgument(object? value)
    {
        Value = value;
    }

    public bool ValueEquals(in UnturnedTestArgument other)
    {
        return Equals(Value, other.Value);
    }

    /// <inheritdoc />
    public override string ToString() => Value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is UnturnedTestArgument a && Equals(Value, a.Value);
    }

    public bool Equals(UnturnedTestArgument other) => Equals(Value, other.Value);

    /// <inheritdoc />
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}