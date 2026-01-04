using System;
using System.Globalization;
using System.Reflection;

namespace uTest.Discovery;

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public class UnturnedTest : ITypeParamsProvider
{
    internal UnturnedTestOwnerInfo? Owner;

    public required string ManagedType { get; init; }
    public required string ManagedMethod { get; init; }
    public required string DisplayName { get; init; }
    public required string Uid { get; init; }
    public required string TreePath { get; init; }
    public string[]? Categories { get; init; }
    public required MethodInfo Method { get; init; }
    public required string? Map { get; init; }
    public required ulong[] WorkshopItems { get; init; }

    /// <summary>
    /// Whether or not this test will have to be expanded into multiple tests (due to parameters, type parameters, etc).
    /// </summary>
    public required bool Expandable { get; init; }
    public string? ParentUid { get; init; }
    public required UnturnedTestParameter[] Parameters { get; init; }
    public required UnturnedTestArgs[] Args { get; init; }

    public UnturnedTestParameter[]? TypeParameters { get; init; }
    public UnturnedTestArgs[]? TypeArgs { get; init; }

    /// <inheritdoc />
    public override string ToString() => Uid;
}

#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
internal interface ITypeParamsProvider
{
    UnturnedTestParameter[]? TypeParameters { get; }
    UnturnedTestArgs[]? TypeArgs { get; }
    string DisplayName { get; }
}

#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
internal class UnturnedTestOwnerInfo : ITypeParamsProvider
{
    internal Type Type;
    public UnturnedTestParameter[]? TypeParameters { get; set; }
    public UnturnedTestArgs[]? TypeArgs { get; set; }

    string ITypeParamsProvider.DisplayName => ManagedIdentifier.GetManagedType(Type);

    public UnturnedTestOwnerInfo(Type type)
    {
        Type = type;
    }
}

/// <summary>
/// Internal API used by source-generator.
/// </summary>
#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public sealed class UnturnedTestArgs
{
    internal bool IsValid;

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

#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
internal interface IUnturnedTestRangeParameter
{
    UnturnedTestSetParameterInfo SetParameterInfo { get; }
    void Visit<TVisitor>(ref TVisitor visitor) where TVisitor : IUnturnedTestRangeParameterVisitor;
}

#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
internal interface IUnturnedTestRangeParameter<out T, out TStep> : IUnturnedTestRangeParameter
    where T : unmanaged, IConvertible
    where TStep : unmanaged, IComparable<TStep>, IConvertible
{
    T From { get; }
    T To { get; }
    TStep Step { get; }
}

#if RELEASE
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
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