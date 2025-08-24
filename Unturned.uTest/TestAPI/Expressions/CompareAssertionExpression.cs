using System;

namespace uTest;

/// <summary>
/// An assertion expression that compares two values.
/// </summary>
public interface ICompareAssertionExpression<in TIn> : ITerminalAssertionExpression<TIn?, bool>;

/// <summary>
/// An assertion expression that compares two values with a tolerance set.
/// </summary>
public interface ICompareAssertionExpressionWithTolerance<in TIn> : ICompareAssertionExpression<TIn>, IAssertionExpressionWithTolerance<TIn>;

internal class CompareAssertionExpression<TIn> : ToleranceAssertionExpression<TIn>, ICompareAssertionExpressionWithTolerance<TIn>
{
    private readonly TIn? _comparand;
    private readonly int _compareType;
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

    public CompareAssertionExpression(TIn? comparand, /* -2 lt, -1 lte, 1 gte, 2 gt */ int compareType)
    {
        _comparand = comparand;
        _compareType = compareType;
    }


    /// <inheritdoc />
    public bool Solve()
    {
        if (!_hasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));
        
        int cmp = EqualityHelper.Comparer<TIn>().Compare(Value, _comparand);

        return _compareType switch
        {
            -2 => cmp < 0,
            -1 => cmp <= 0,
            0 => false,
            1 => cmp >= 0,
            2 => cmp > 0,

            _ => false
        };
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