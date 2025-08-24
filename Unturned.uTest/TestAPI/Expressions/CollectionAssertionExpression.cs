using System;

namespace uTest;

/// <summary>
/// Base helper class for all collection assertion expressions.
/// </summary>
internal abstract class CollectionAssertionExpression<TElementType> : ITerminalAssertionExpression<IEnumerable<TElementType>, bool>
{
    protected bool HasSetValue;

    /// <inheritdoc />
    public IAssertionExpression? Child { get; set; }

    /// <inheritdoc />
    public IEnumerable<TElementType>? Value
    {
        get;
        set
        {
            HasSetValue = true;
            field = value;
        }
    }

    public abstract bool Solve();

    /// <inheritdoc />
    public void VisitConsumer<TConsumerVisitor>(ref TConsumerVisitor visitor) where TConsumerVisitor : IAssertionExpressionConsumerVisitor { }

    /// <inheritdoc />
    public void VisitProducer<TProducerVisitor>(ref TProducerVisitor visitor) where TProducerVisitor : IAssertionExpressionProducerVisitor
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Base helper class for all collection assertion expressions that contain a nested expression.
/// </summary>
internal abstract class ExpressionCollectionAssertionExpression<TElementType> : CollectionAssertionExpression<TElementType>
{
    public ITerminalAssertionExpression<TElementType, bool>? Expression { get; set; }

    /// <inheritdoc />
    public override bool Solve()
    {
        if (!HasSetValue || Expression == null)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        try
        {
            return Evaluate(Value);
        }
        finally
        {
            Expression.Value = default;
        }
    }

    protected abstract bool Evaluate(IEnumerable<TElementType>? value);
}

internal sealed class AllAssertionExpression<TElementType> : ExpressionCollectionAssertionExpression<TElementType>
{
    protected override bool Evaluate(IEnumerable<TElementType>? value)
    {
        if (value == null)
            return true;

        bool allMet = true;
        foreach (TElementType element in value)
        {
            Expression!.Value = element;
            if (Expression.Solve())
                continue;

            allMet = false;
            break;
        }

        return allMet;
    }
}

internal sealed class AnyAssertionExpression<TElementType> : ExpressionCollectionAssertionExpression<TElementType>
{
    protected override bool Evaluate(IEnumerable<TElementType>? value)
    {
        if (value == null)
            return false;

        bool anyMet = false;
        foreach (TElementType element in value)
        {
            Expression!.Value = element;
            if (!Expression.Solve())
                continue;

            anyMet = true;
            break;
        }

        return anyMet;
    }
}

internal sealed class AtLeastAssertionExpression<TElementType>(ulong count) : ExpressionCollectionAssertionExpression<TElementType>
{
    private readonly ulong _count = count;

    protected override bool Evaluate(IEnumerable<TElementType>? value)
    {
        if (value == null)
            return false;

        ulong metCt = 0;
        foreach (TElementType element in value)
        {
            Expression!.Value = element;
            if (!Expression.Solve())
                continue;

            ++metCt;
            if (metCt >= _count)
                return true;
        }

        return false;
    }
}

internal sealed class AtMostAssertionExpression<TElementType>(ulong count) : ExpressionCollectionAssertionExpression<TElementType>
{
    private readonly ulong _count = count;

    protected override bool Evaluate(IEnumerable<TElementType>? value)
    {
        if (value == null)
            return true;

        ulong metCt = 0;
        foreach (TElementType element in value)
        {
            Expression!.Value = element;
            if (!Expression.Solve())
                continue;

            ++metCt;
            if (metCt > _count)
                return false;
        }

        return true;
    }
}

internal sealed class UniqueAssertionExpression<TElementType> : CollectionAssertionExpression<TElementType>, IEqualityComparerAssertionExpression<TElementType>
{
    private IEqualityComparer<TElementType>? _comparer;

    /// <inheritdoc />
    public IEqualityComparerAssertionExpression<TElementType> Using(IEqualityComparer<TElementType> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        return this;
    }

    /// <inheritdoc />
    public override bool Solve()
    {
        if (!HasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        IEnumerable<TElementType>? value = Value;

        if (value == null)
            return true;

        if (value is string str)
        {
            return EqualityHelper.StringUnique(str);
        }

        HashSet<TElementType> hashSet = new HashSet<TElementType>(_comparer ?? EqualityHelper.Default<TElementType>());

        foreach (TElementType element in value)
        {
            if (!hashSet.Add(element))
                return false;
        }

        return true;
    }
}

internal sealed class EquivalentCollectionAssertionExpression<TElementType>(IEnumerable<TElementType>? enumerable) : CollectionAssertionExpression<TElementType>, IEqualityComparerAssertionExpression<TElementType>
{
    private IEqualityComparer<TElementType>? _comparer;

    /// <inheritdoc />
    public IEqualityComparerAssertionExpression<TElementType> Using(IEqualityComparer<TElementType> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        return this;
    }

    /// <inheritdoc />
    public override bool Solve()
    {
        if (!HasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        IEnumerable<TElementType>? value = Value;
        if (value == null)
            return enumerable == null;

        if (enumerable == null)
            return false;

        if (value is string str && enumerable is string str2)
        {
            return string.Equals(str, str2, StringComparison.Ordinal);
        }

        if (value is ICollection c1 && enumerable is ICollection c2)
        {
            if (c1.Count != c2.Count)
                return false;
        }

        using IEnumerator<TElementType> e1 = value.GetEnumerator();
        using IEnumerator<TElementType> e2 = enumerable.GetEnumerator();

        IEqualityComparer<TElementType> comparer = _comparer ?? EqualityHelper.Default<TElementType>();

        while (true)
        {
            bool s1 = e1.MoveNext();
            bool s2 = e2.MoveNext();
            if (s1 != s2)
                return false;
            if (!s1)
                break;

            if (!comparer.Equals(e1.Current, e2.Current))
                return false;
        }

        return true;
    }
}

internal sealed class SubsetOrSupersetCollectionAssertionExpression<TElementType>(IEnumerable<TElementType>? enumerable, bool isSuperset) : CollectionAssertionExpression<TElementType>, IEqualityComparerAssertionExpression<TElementType>
{
    private readonly IEnumerable<TElementType>? _enumerable = enumerable;
    private IEqualityComparer<TElementType>? _comparer;

    /// <inheritdoc />
    public IEqualityComparerAssertionExpression<TElementType> Using(IEqualityComparer<TElementType> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        return this;
    }

    /// <inheritdoc />
    public override bool Solve()
    {
        if (!HasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        IEnumerable<TElementType>? largeSet = Value;
        IEnumerable<TElementType>? smallSet = _enumerable;

        if (isSuperset)
            (largeSet, smallSet) = (smallSet, largeSet);

        if (largeSet == null)
            return smallSet == null;

        if (smallSet == null)
            return false;
        
        if (largeSet is ICollection c1 && smallSet is ICollection c2)
        {
            if (c1.Count < c2.Count)
                return false;
            if (c2.Count == 0)
                return true;
        }

        IEqualityComparer<TElementType> comparer = _comparer ?? EqualityHelper.Default<TElementType>();

        HashSet<TElementType> superset = new HashSet<TElementType>(comparer);
        foreach (TElementType element in largeSet)
        {
            superset.Add(element);
        }

        foreach (TElementType subsetElement in smallSet)
        {
            if (!superset.Contains(subsetElement))
                return false;
        }

        return true;
    }
}

/// <summary>
/// An assertion expression that checks if a collection is ordered.
/// </summary>
public interface IOrderedCollectionTerminalAssertionExpression<TElementType> : ITerminalAssertionExpression<IEnumerable<TElementType>, bool>
{
    /// <summary>
    /// Indicates that the actual value should be ordered in descending order.
    /// </summary>
    IOrderedCollectionTerminalAssertionExpression<TElementType> Descending { get; }

    /// <summary>
    /// Incidcates that ordering comparisons should use this <see cref="IComparer{T}"/>.
    /// </summary>
    IOrderedCollectionTerminalAssertionExpression<TElementType> Using(IComparer<TElementType?> comparer);

    /// <summary>
    /// Incidcates that ordering comparisons should use this <see cref="Comparison{T}"/>.
    /// </summary>
    IOrderedCollectionTerminalAssertionExpression<TElementType> Using(Comparison<TElementType?> comparer);

    // TODO: not going to add sorting by properties right now
}

internal sealed class OrderedCollectionAssertionExpression<TElementType>(bool descending) : CollectionAssertionExpression<TElementType>, IOrderedCollectionTerminalAssertionExpression<TElementType>
{
    private bool _descending = descending;
    private IComparer<TElementType?>? _comparer;
    private Comparison<TElementType?>? _comparison;

    /// <inheritdoc />
    public IOrderedCollectionTerminalAssertionExpression<TElementType> Descending
    {
        get
        {
            _descending = true;
            return this;
        }
    }

    /// <inheritdoc />
    public IOrderedCollectionTerminalAssertionExpression<TElementType> Using(IComparer<TElementType?> comparer)
    {
        _comparer = comparer;
        return this;
    }

    /// <inheritdoc />
    public IOrderedCollectionTerminalAssertionExpression<TElementType> Using(Comparison<TElementType?> comparer)
    {
        _comparison = comparer;
        return this;
    }

    /// <inheritdoc />
    public override bool Solve()
    {
        if (!HasSetValue)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        IEnumerable<TElementType>? value = Value;
        if (value == null)
            return false;
        
        if (value is ICollection { Count: <= 1 })
            return true;

        int cmpExpected = _descending ? -1 : 1;
        TElementType? last = default;
        bool hasLast = false;

        if (_comparison != null)
        {
            Comparison<TElementType?> comparison = _comparison;

            foreach (TElementType element in value)
            {
                if (!hasLast)
                {
                    last = element;
                    hasLast = true;
                    continue;
                }

                int cmp = comparison(last, element);
                if (cmp != 0 && Math.Sign(cmp) != cmpExpected)
                    return false;
            }
        }
        else
        {
            IComparer<TElementType?> comparer = _comparer ?? EqualityHelper.Comparer<TElementType?>();

            foreach (TElementType element in value)
            {
                if (!hasLast)
                {
                    last = element;
                    hasLast = true;
                    continue;
                }

                int cmp = comparer.Compare(last, element);
                if (cmp != 0 && Math.Sign(cmp) != cmpExpected)
                    return false;
            }
        }

        return true;
    }
}