using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using System.Reflection;
using IMTPLogger = Microsoft.Testing.Platform.Logging.ILogger;

namespace uTest.Runner;

internal interface ITestRegistrationList : ITestFrameworkCapability
{
    Task<List<UnturnedTestInstance>> GetMatchingTestsAsync(ITestExecutionFilter filter, IMTPLogger logger, CancellationToken token = default);

    Task<List<UnturnedTest>> GetTestsAsync(IMTPLogger logger, CancellationToken token = default);

    Task<List<UnturnedTestInstance>> ExpandTestsAsync(IMTPLogger logger, List<UnturnedTest> originalTests, ITestExecutionFilter? filter, CancellationToken token = default);
}

internal readonly struct UnturnedTestInstance
{
    internal static UnturnedTestInstance Null = default;

    public Type Type { get; }
    public MethodInfo Method { get; }
    public Type[] TypeArgs { get; }
    public Type[] MethodTypeArgs { get; }
    public UnturnedTest Test { get; }
    public object?[] Arguments { get; }

    public string ManagedType { get; }
    public string ManagedMethod { get; }

    public string Uid { get; }
    public string DisplayName { get; }
    public string TreePath { get; }
    public int Index { get; }
    public int ArgHash { get; }
    public bool HasParameters => Arguments.Length > 0;

    /// <summary>
    /// Must only be used with an unexpandable test.
    /// </summary>
    /// <exception cref="ArgumentException">The given <paramref name="test"/> is expandable.</exception>
    public UnturnedTestInstance(UnturnedTest test)
    {
        if (test.Expandable)
            throw new ArgumentException("Expandable test.", nameof(test));

        Type = test.Owner?.Type ?? test.Method.DeclaringType!;
        Method = test.Method;
        TypeArgs = Type.EmptyTypes;
        MethodTypeArgs = Type.EmptyTypes;
        Test = test;
        Arguments = Array.Empty<object?>();
        
        Index = 0;
        ArgHash = TestExpandProcessor.DefaultArgHash;

        ManagedType = test.ManagedType;
        ManagedMethod = test.ManagedMethod;

        Uid = test.Uid;
        DisplayName = test.DisplayName;
        TreePath = test.TreePath;
    }

    public UnturnedTestInstance(TestExpandProcessor processor, UnturnedTest test, Type type, MethodInfo method, string managedType, string managedMethod, object?[] arguments, int index, int argHash, Type[] typeArgs, Type[] methodTypeArgs)
    {
        Type = type;
        Method = method;
        TypeArgs = typeArgs;
        MethodTypeArgs = methodTypeArgs;
        Test = test;
        Arguments = arguments;

        Index = index;
        ArgHash = argHash;

        ManagedType = managedType;
        ManagedMethod = managedMethod;

        // has to go last, this method relies on the other properties
        processor.GetNames(in this, out string uid, out string displayName, out string treePath);
        Uid = uid;
        DisplayName = displayName;
        TreePath = treePath;
    }

    internal static int CalculateArgumentHash(Type[] typeArgs, Type[] methodTypeArgs, object?[] arguments)
    {
        int hashCode = -2128831035;
        foreach (Type arg in typeArgs)
        {
            hashCode = unchecked( (hashCode ^ arg.GetHashCode()) * 16777619 );
        }
        foreach (Type arg in methodTypeArgs)
        {
            hashCode = unchecked( (hashCode ^ arg.GetHashCode()) * 16777619 );
        }
        foreach (object? arg in arguments)
        {
            if (arg != null)
                hashCode = unchecked ( (hashCode ^ arg.GetHashCode()) * 16777619 );
            else
                hashCode = ~hashCode;
        }

        return hashCode;
    }

    public void AddProperties(PropertyBag bag)
    {
        if (Test.LocationInfo != null)
            bag.Add(Test.LocationInfo);
        if (Test.IdentifierInfo != null)
            bag.Add(Test.IdentifierInfo);
    }

    public TestNode CreateTestNode(out TestNodeUid? parentUid)
    {
        TestNode node = new TestNode
        {
            DisplayName = DisplayName,
            Uid = new TestNodeUid(Uid)
        };

        AddProperties(node.Properties);

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