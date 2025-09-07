using System;

namespace uTest;

internal class DoubleSignAssertionExpression : MutationAssertionExpression<double, bool>, ISignAssertionExpression<double>
{
    public DoubleSignAssertionExpression(int sign) : base(x => Math.Sign(x) == sign) { }
}

internal class DecimalSignAssertionExpression : MutationAssertionExpression<decimal, bool>, ISignAssertionExpression<decimal>
{
    public DecimalSignAssertionExpression(int sign) : base(x => Math.Sign(x) == sign) { }
}

/// <summary>
/// An <see cref="ITerminalAssertionExpression{TIn,TOut}"/> that produces a value based on a mutator function applied to the value it consumes.
/// </summary>
public class TerminalMutationAssertionExpression<TIn, TOut>(Func<TIn, TOut> func)
    : MutationAssertionExpression<TIn, TOut>(func), ITerminalAssertionExpression<TIn, TOut>;

/// <summary>
/// An <see cref="INonTerminalAssertionExpression{TIn,TOut}"/> that produces a value based on a mutator function applied to the value it consumes.
/// </summary>
public class MutationAssertionExpression<TIn, TOut> : INonTerminalAssertionExpression<TIn, TOut>, ISpecialBehaviorAssertionExpression
{
    private readonly Func<TIn, TOut> _func;

    internal SpecialBehavior SpecialBehavior;

    SpecialBehavior ISpecialBehaviorAssertionExpression.SpecialBehavior => SpecialBehavior;

    /// <inheritdoc />
    public IAssertionExpressionParent<TIn>? Parent { get; set; }

    /// <inheritdoc />
    public IAssertionExpression? Child { get; set; }

    public TIn? Value
    {
        set
        {
            if (Parent is ConstantAssertionExpression<TIn> c)
                c.Value = value!;
            else
                Parent = new ConstantAssertionExpression<TIn>(value!);
        }
    }

    public MutationAssertionExpression(Func<TIn, TOut> func)
    {
        _func = func;
    }


    /// <inheritdoc />
    public TOut Solve()
    {
        if (Parent == null)
            throw new InvalidOperationException(string.Format(Properties.Resources.AssertionExpressionMissingParent, this));

        return _func(Parent.Solve());
    }

    /// <inheritdoc cref="object.ToString" />
    public override string ToString()
    {
        return ""; // todo
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