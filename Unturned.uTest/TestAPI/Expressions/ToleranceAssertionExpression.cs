using System;

namespace uTest;

/// <summary>
/// An assertion expression that has a tolerance.
/// </summary>
public interface IAssertionExpressionWithTolerance<in TIn>
{
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    ITolerance? Tolerance { get; }

#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    void SetTolerance<TValueType>(Tolerance<TValueType> tolerance) where TValueType : IComparable<TValueType>, IEquatable<TValueType>;
}

internal class ToleranceAssertionExpression<TIn> : IAssertionExpressionWithTolerance<TIn>
{
    /// <inheritdoc />
    public ITolerance? Tolerance { get; private set; }

    /// <inheritdoc />
    public void SetTolerance<TValueType>(Tolerance<TValueType> tolerance) where TValueType : IComparable<TValueType>, IEquatable<TValueType>
    {
        Tolerance = tolerance;
    }
}
