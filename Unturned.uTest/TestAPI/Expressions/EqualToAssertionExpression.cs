using System;

namespace uTest;

/// <summary>
/// An assertion expression that compares two values.
/// </summary>
public interface IEqualToAssertionExpression<in TIn> : ICompareAssertionExpression<TIn>;

/// <summary>
/// An assertion expression that compares two values with a tolerance set.
/// </summary>
public interface IEqualToAssertionExpressionWithTolerance<in TIn> : ICompareAssertionExpressionWithTolerance<TIn>, IEqualToAssertionExpression<TIn>;

internal class EqualToAssertionExpression<TIn> : ToleranceAssertionExpression<TIn>, IEqualToAssertionExpressionWithTolerance<TIn>
{
    private readonly TIn? _comparand;
    private bool _hasSetValue;
    
    /// <summary>
    /// Overrides the comparer to use. If this is set tolerance is ignored.
    /// </summary>
    public IEqualityComparer<TIn>? Comparer { get; internal set; }

    /// <inheritdoc />
    public IAssertionExpression? Child { get; set; }

    /// <inheritdoc />
    public TIn? Value
    {
        get;
        set
        {
            _hasSetValue = true;
            field = value;
        }
    }

    public EqualToAssertionExpression(TIn? comparand)
    {
        _comparand = comparand;
    }


    /// <inheritdoc />
    public bool Solve()
    {
        if (!_hasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        return EqualityHelper.Default<TIn>().Equals(Value, _comparand);
    }

    /// <inheritdoc cref="object.ToString" />
    public override string ToString()
    {
        return ""; // todo
    }

    /// <inheritdoc />
    public void VisitConsumer<TConsumerVisitor>(ref TConsumerVisitor visitor) where TConsumerVisitor : IAssertionExpressionConsumerVisitor { }

    /// <inheritdoc />
    public void VisitProducer<TProducerVisitor>(ref TProducerVisitor visitor) where TProducerVisitor : IAssertionExpressionProducerVisitor
    {
        visitor.Visit(this);
    }
}