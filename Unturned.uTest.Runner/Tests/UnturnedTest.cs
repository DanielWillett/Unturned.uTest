using System.Globalization;
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
    public required UnturnedTestParameter[] Parameters { get; init; }
    public required UnturnedTestArgs[] Args { get; init; }

    public TestMethodIdentifierProperty? IdentifierInfo { get; init; }
    public TestFileLocationProperty? LocationInfo { get; init; }


    /// <inheritdoc />
    public override string ToString() => Uid;

    public TestNode CreateTestNode()
    {
        TestNode node = new TestNode
        {
            DisplayName = DisplayName,
            Uid = new TestNodeUid(Uid)
        };

        if (LocationInfo != null)
            node.Properties.Add(LocationInfo);
        if (IdentifierInfo != null)
            node.Properties.Add(IdentifierInfo);

        return node;
    }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public class UnturnedTestArgs
{
    public string? From { get; init; }
    public object?[]? Values { get; init; }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public abstract class UnturnedTestParameter
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public required bool IsByRef { get; init; }
    public required int Position { get; init; }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestSetParameter : UnturnedTestParameter
{
    public string? From { get; init; }
    public object?[]? Values { get; init; }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestRangeParameter<T> : UnturnedTestParameter
    where T : unmanaged, IComparable<T>, IConvertible, IFormattable
{
    private static readonly bool IsValidType
        = typeof(T).IsPrimitive
          && typeof(T) != typeof(bool)
          && typeof(T) != typeof(IntPtr)
          && typeof(T) != typeof(UIntPtr);
    
    public UnturnedTestRangeParameter()
    {
        if (!IsValidType)
            throw new InvalidOperationException($"""Type "{typeof(T).AssemblyQualifiedName}" is not a valid range type.""");
    }

    public required T From { get; init; }
    public required T To { get; init; }
    public required T Step { get; init; }

    public override string ToString()
    {
        return $"{From.ToString(null, CultureInfo.InvariantCulture)} " +
               $"- {To.ToString(null, CultureInfo.InvariantCulture)} " +
               $"[step={Step.ToString(null, CultureInfo.InvariantCulture)}]";
    }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestRangeEnumParameter<T, TUnderlying> : UnturnedTestParameter
    where T : unmanaged, Enum
    where TUnderlying : unmanaged, IComparable<T>, IConvertible, IFormattable
{
    public required T From { get; init; }
    public required string FromFieldName { get; init; }
    public required TUnderlying FromUnderlying { get; init; }

    public required T To { get; init; }
    public required string ToFieldName { get; init; }
    public required TUnderlying ToUnderlying { get; init; }

    public override string ToString() => $"{typeof(T).Name}.{FromFieldName} - {typeof(T).Name}.{ToFieldName}";
}