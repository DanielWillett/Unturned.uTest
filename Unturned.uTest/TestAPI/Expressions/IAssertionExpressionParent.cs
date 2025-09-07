using System;
using System.ComponentModel;

namespace uTest;

/// <summary>
/// An expression that can not have basic expressions appended to it.
/// </summary>
public interface ITerminalAssertionExpression<in TIn, out TOut> : IAssertionExpression<TIn, TOut>
{
    /// <summary>
    /// The value to calculate against.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    TIn? Value { set; }
}

/// <summary>
/// An expression that should have basic expressions appended to it.
/// </summary>
public interface INonTerminalAssertionExpression<TIn, out TOut> : IAssertionExpression<TIn, TOut>, IAssertionExpressionChild<TIn>;

/// <summary>
/// Base interface for an assertion expression used to decide whether an assertion is met.
/// </summary>
public interface IAssertionExpression
{
    /// <summary>
    /// Invokes a <see cref="IAssertionExpressionConsumerVisitor"/> if this expression is a consumer, otherwise nothing happens.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    void VisitConsumer<TConsumerVisitor>(ref TConsumerVisitor visitor) where TConsumerVisitor : IAssertionExpressionConsumerVisitor;

    /// <summary>
    /// Invokes a <see cref="IAssertionExpressionProducerVisitor"/> if this expression is a producer, otherwise nothing happens.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    void VisitProducer<TProducerVisitor>(ref TProducerVisitor visitor) where TProducerVisitor : IAssertionExpressionProducerVisitor;

    /// <inheritdoc cref="object.GetHashCode"/>
    /// <remarks>Hidden by <see cref="IAssertionExpression"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    int GetHashCode();

    /// <inheritdoc cref="object.Equals(object)"/>
    /// <remarks>Hidden by <see cref="IAssertionExpression"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    bool Equals(object? other);

    /// <inheritdoc cref="object.ToString"/>
    /// <remarks>Hidden by <see cref="IAssertionExpression"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    string ToString();

    /// <inheritdoc cref="object.GetType"/>
    /// <remarks>Hidden by <see cref="IAssertionExpression"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    Type GetType();
}

/// <summary>
/// Visitor for <see cref="IAssertionExpression.VisitConsumer{TConsumerVisitor}"/>.
/// </summary>
public interface IAssertionExpressionConsumerVisitor
{
    /// <summary>
    /// Invoked by the consumer when <see cref="IAssertionExpression.VisitConsumer{TConsumerVisitor}"/> is invoked.
    /// </summary>
    void Visit<TIn>(IAssertionExpressionChild<TIn> consumer);
}

/// <summary>
/// Visitor for <see cref="IAssertionExpression.VisitProducer{TProducerVisitor}"/>.
/// </summary>
public interface IAssertionExpressionProducerVisitor
{
    /// <summary>
    /// Invoked by the consumer when <see cref="IAssertionExpression.VisitProducer{TProducerVisitor}"/> is invoked.
    /// </summary>
    void Visit<TIn>(IAssertionExpressionParent<TIn> producer);
}

/// <summary>
/// An assertion expression that produces a value.
/// </summary>
public interface IAssertionExpressionParent<out TOut> : IAssertionExpression
{
    /// <summary>
    /// The child consuming this expression's value.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    IAssertionExpression? Child { get; set; }

    /// <summary>
    /// Evaluate this expression based on the parent.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    TOut Solve();
}

/// <summary>
/// An assertion expression that produces a value based on a consumed value. Assertion expressions are used to decide whether or not an assertion is met.
/// </summary>
// ReSharper disable once UnusedTypeParameter
public interface IAssertionExpression<in TIn, out TOut> : IAssertionExpressionParent<TOut>;

/// <summary>
/// An assertion expression that consumes a value.
/// </summary>
public interface IAssertionExpressionChild<TIn> : IAssertionExpression
{
    /// <summary>
    /// The parent that produces the value this expression consumes.
    /// </summary>
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    IAssertionExpressionParent<TIn>? Parent { get; set; }
}

internal interface IEqualityComparerAssertionExpression<TElementType> : ITerminalAssertionExpression<IEnumerable<TElementType>, bool>
{
    /// <summary>
    /// Incidcates that equality comparisons should use this <see cref="IEqualityComparer{T}"/>.
    /// </summary>
    IEqualityComparerAssertionExpression<TElementType> Using(IEqualityComparer<TElementType> comparer);
}

internal interface ISpecialBehaviorAssertionExpression : IAssertionExpression
{
#if RELEASE
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    SpecialBehavior SpecialBehavior { get; }
}

/// <summary>
/// An assertion expression that checks the sign of a number. This can be extended to check for positive or negative infinity or zero.
/// </summary>
/// <typeparam name="TDecimalType"><see cref="double"/> or <see cref="decimal"/></typeparam>
public interface ISignAssertionExpression<TDecimalType> : ITerminalAssertionExpression<TDecimalType, bool>, INonTerminalAssertionExpression<TDecimalType, bool> where TDecimalType : unmanaged;

/// <summary>
/// An <see cref="INonTerminalAssertionExpression{TIn,TOut}"/> that produces the same value as it consumes.
/// </summary>
public class IdentityAssertionExpression<T> : INonTerminalAssertionExpression<T?, T?>
{
    /// <inheritdoc />
    public IAssertionExpressionParent<T?>? Parent { get; set; }

    /// <inheritdoc />
    public IAssertionExpression? Child { get; set; }

    public T? Solve()
    {
        if (Parent == null)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        return Parent.Solve();
    }

    /// <inheritdoc />
    public void VisitConsumer<TConsumerVisitor>(ref TConsumerVisitor visitor) where TConsumerVisitor : IAssertionExpressionConsumerVisitor
    {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public void VisitProducer<TProducerVisitor>(ref TProducerVisitor visitor) where TProducerVisitor : IAssertionExpressionProducerVisitor
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// An <see cref="IAssertionExpressionParent{TOut}"/> that produces a constant value.
/// </summary>
public class ConstantAssertionExpression<TOut> : IAssertionExpressionParent<TOut>
{
    public TOut Value { get; internal set; }

    /// <inheritdoc />
    public IAssertionExpression? Child { get; set; }

    public ConstantAssertionExpression(TOut value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public TOut Solve() => Value;

    /// <inheritdoc />
    public void VisitConsumer<TConsumerVisitor>(ref TConsumerVisitor visitor) where TConsumerVisitor : IAssertionExpressionConsumerVisitor { }

    /// <inheritdoc />
    public void VisitProducer<TProducerVisitor>(ref TProducerVisitor visitor) where TProducerVisitor : IAssertionExpressionProducerVisitor
    {
        visitor.Visit(this);
    }
}

internal enum SpecialBehavior
{
    None,
    Not,
    Positive,
    Negative
}