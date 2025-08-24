using System;

namespace uTest;

public interface ITolerance
{
    void VisitTolerance<TVisitor>(ref TVisitor visitor) where TVisitor : IToleranceVisitor;
}

public interface IToleranceVisitor
{
    void Visit<TValueType>(Tolerance<TValueType> tolerance) where TValueType : IComparable<TValueType>, IEquatable<TValueType>;
}

public sealed class Tolerance<TValueType> : ITolerance where TValueType : IComparable<TValueType>, IEquatable<TValueType>
{
    public TValueType Value { get; internal set; }
    public ToleranceUnit Unit { get; internal set; }

    public Tolerance(TValueType value, ToleranceUnit unit = ToleranceUnit.Linear)
    {
        Value = value;
        Unit = unit;
    }

    public void VisitTolerance<TVisitor>(ref TVisitor visitor) where TVisitor : IToleranceVisitor
    {
        visitor.Visit(this);
    }
}

public enum ToleranceUnit
{
    Unset,
    Linear,
    Percent,
    Ulps
}