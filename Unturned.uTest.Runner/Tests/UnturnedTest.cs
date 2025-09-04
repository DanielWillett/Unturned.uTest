using Microsoft.Testing.Platform.Extensions.Messages;
using System.Globalization;
using System.Reflection;

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

    public UnturnedTestParameter[]? TypeParameters { get; init; }


    /// <inheritdoc />
    public override string ToString() => Uid;
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestArgs
{
    public string? From { get; init; }
    public Array? Values { get; init; }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public class UnturnedTestParameter
{
    public required string Name { get; init; }
    public bool IsByRef { get; init; }
    public required int Position { get; init; }

    protected internal virtual bool TryGetValues(out Array? values, out string? from)
    {
        from = null;
        values = null;
        return false;
    }
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
    public Array? Values { get; init; }

    /// <inheritdoc />
    protected internal override bool TryGetValues(out Array? values, out string? from)
    {
        if (!string.IsNullOrWhiteSpace(From))
        {
            values = null;
            from = From;
        }
        else
        {
            values = Values;
            from = null;
        }

        return values != null || from != null;
    }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public readonly struct UnturnedTestSetParameterInfo
{
    public string? From { get; init; }
    public Array? Values { get; init; }
}

internal interface IUnturnedTestRangeParameter
{
    UnturnedTestSetParameterInfo SetParameterInfo { get; }
    void Visit<TVisitor>(ref TVisitor visitor) where TVisitor : IUnturnedTestRangeParameterVisitor;
}

internal interface IUnturnedTestRangeParameter<out T, out TStep> : IUnturnedTestRangeParameter
    where T : unmanaged, IConvertible
    where TStep : unmanaged, IComparable<TStep>, IConvertible
{
    T From { get; }
    T To { get; }
    TStep Step { get; }
}

internal interface IUnturnedTestRangeParameterVisitor
{
    void Visit<T, TStep>(IUnturnedTestRangeParameter<T, TStep> parameter)
        where T : unmanaged, IConvertible
        where TStep : unmanaged, IComparable<TStep>, IConvertible;
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestRangeParameter<T> : UnturnedTestParameter, IUnturnedTestRangeParameter<T, T>
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

    public UnturnedTestSetParameterInfo SetParameterInfo { get; init; }

    void IUnturnedTestRangeParameter.Visit<TVisitor>(ref TVisitor visitor)
    {
        visitor.Visit(this);
    }

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
public sealed class UnturnedTestRangeCharParameter : UnturnedTestParameter, IUnturnedTestRangeParameter<char, int>
{
    public required char From { get; init; }
    public required char To { get; init; }
    public required int Step { get; init; }

    public UnturnedTestSetParameterInfo SetParameterInfo { get; init; }

    void IUnturnedTestRangeParameter.Visit<TVisitor>(ref TVisitor visitor)
    {
        visitor.Visit(this);
    }

    public override string ToString()
    {
        return $"{From.ToString()} " +
               $"- {To.ToString()} " +
               $"[step={Step.ToString(null, CultureInfo.InvariantCulture)}]";
    }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestRangeEnumParameter<T, TUnderlying> : UnturnedTestParameter, IUnturnedTestRangeParameter<T, int>
    where T : unmanaged, Enum
    where TUnderlying : unmanaged, IComparable<TUnderlying>, IConvertible
{
    public required T From { get; init; }
    public required string FromFieldName { get; init; }
    public required TUnderlying FromUnderlying { get; init; }

    public required T To { get; init; }

    int IUnturnedTestRangeParameter<T, int>.Step => 1;

    public required string ToFieldName { get; init; }
    public required TUnderlying ToUnderlying { get; init; }

    public UnturnedTestSetParameterInfo SetParameterInfo { get; init; }

    void IUnturnedTestRangeParameter.Visit<TVisitor>(ref TVisitor visitor)
    {
        visitor.Visit(this);
    }

    public override string ToString() => $"{typeof(T).Name}.{FromFieldName} - {typeof(T).Name}.{ToFieldName}";
}