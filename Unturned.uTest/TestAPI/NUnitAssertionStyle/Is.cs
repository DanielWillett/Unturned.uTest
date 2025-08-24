using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.Serialization;

#pragma warning disable IDE0130

namespace uTest.Assertions.NUnit;

#pragma warning restore IDE0130

/// <summary>
/// Similar to the 'Is' class in NUnit. Contains expressions that are met when something *is* another expression. Some expressions such as <see cref="Not"/> can be chained.
/// </summary>
[DebuggerStepThrough, ExcludeFromCodeCoverage, DebuggerNonUserCode]
public static class Is
{
    /// <summary>
    /// Numeric comparison expressions for <see cref="decimal"/> numbers.
    /// </summary>
    public static INonTerminalAssertionExpression<decimal, decimal> Decimal { get; } = new IdentityAssertionExpression<decimal>();

    /// <summary>
    /// Inverts the expression following this one.
    /// </summary>
    public static INonTerminalAssertionExpression<bool, bool> Not => new MutationAssertionExpression<bool, bool>(x => !x) { SpecialBehavior = SpecialBehavior.Not };

    /// <summary>
    /// Expression that is met when an object is null. Note that this is invalid when used with non-nullable value types and returns false.
    /// </summary>
    public static ITerminalAssertionExpression<object, bool> Null => new TerminalMutationAssertionExpression<object, bool>(x => x == null);

    /// <summary>
    /// Expression that is met when an object is equal to the default value for it's type. When used with a reference type or nullable value type, this behaves the same as <see cref="Null"/>.
    /// </summary>
    public static ITerminalAssertionExpression<object, bool> Default => new TerminalMutationAssertionExpression<object, bool>(x =>
    {
        if (x == null)
            return true;

        Type type = x.GetType();
        if (!type.IsValueType)
            return false;

        try
        {
            // note: Activator.CreateInstance invokes the parameterless cosntructor on value types
            object defaultValue = FormatterServices.GetUninitializedObject(type);
            return EqualityHelper.ValueEquals(x, defaultValue);
        }
        catch
        {
            return false;
        }
    });

    /// <summary>
    /// Expression that is met when a boolean value is true.
    /// </summary>
    public static ITerminalAssertionExpression<bool, bool> True => new TerminalMutationAssertionExpression<bool, bool>(x => x);

    /// <summary>
    /// Expression that is met when a boolean value is false.
    /// </summary>
    public static ITerminalAssertionExpression<bool, bool> False => new TerminalMutationAssertionExpression<bool, bool>(x => !x);

    /// <summary>
    /// Expression that is met when a numeric value is greater than zero.
    /// </summary>
    public static ISignAssertionExpression<double> Positive => new DoubleSignAssertionExpression(1) { SpecialBehavior = SpecialBehavior.Positive };

    /// <summary>
    /// Expression that is met when a numeric value is less than zero.
    /// </summary>
    public static ISignAssertionExpression<double> Negative => new DoubleSignAssertionExpression(-1) { SpecialBehavior = SpecialBehavior.Negative };

    /// <summary>
    /// Expression that is met when a numeric value is zero.
    /// </summary>
    public static ITerminalAssertionExpression<double, bool> Zero => new TerminalMutationAssertionExpression<double, bool>(x => x == 0);

    /// <summary>
    /// Expression that is met when a numeric value is NaN.
    /// </summary>
    public static ITerminalAssertionExpression<double, bool> NaN => new TerminalMutationAssertionExpression<double, bool>(double.IsNaN);

    /// <summary>
    /// Expression that is met when a numeric value is +/- Infinity.
    /// </summary>
    public static ITerminalAssertionExpression<double, bool> Infinity => new TerminalMutationAssertionExpression<double, bool>(double.IsInfinity);

    /// <summary>
    /// Expression that is met when a numeric value is neither NaN or Infinity.
    /// </summary>
    public static ITerminalAssertionExpression<double, bool> Finite => new TerminalMutationAssertionExpression<double, bool>(
#if NETSTANDARD2_1_OR_GREATER
        double.IsFinite
#else
        x => !double.IsNaN(x) && !double.IsInfinity(x)
#endif
    );

    /// <summary>
    /// Expression that is met when a number is odd. This expression will not be met when given a non-rounded decimal value.
    /// </summary>
    /// <remarks>This method can run into precision issues with very large numbers.</remarks>
    public static ITerminalAssertionExpression<double, bool> Odd => new TerminalMutationAssertionExpression<double, bool>(x =>
    {
        x = Math.Abs(x);
        double rnd = Math.Round(x);
        if (Math.Abs(x - rnd) > AssertionExpressionExtensions.RoundTolarance)
            return false;

        return rnd % 2.0 is > 0.5 and < 1.5;
    });

    /// <summary>
    /// Expression that is met when a number is even. This expression will not be met when given a non-rounded decimal value.
    /// </summary>
    /// <remarks>This method can run into precision issues with very large numbers.</remarks>
    public static ITerminalAssertionExpression<double, bool> Even => new TerminalMutationAssertionExpression<double, bool>(x =>
    {
        x = Math.Abs(x);
        double rnd = Math.Round(x);
        if (Math.Abs(x - rnd) > AssertionExpressionExtensions.RoundTolarance)
            return false;

        return rnd % 2.0 is < 0.5 or > 1.5;
    });

    /// <summary>
    /// Expression that is met when a number is a multiple of <paramref name="multiple"/>. This expression will not be met when given a non-rounded decimal value.
    /// </summary>
    /// <remarks>This method can run into precision issues with very large numbers.</remarks>
    [Pure]
    public static ITerminalAssertionExpression<double, bool> MultipleOf(int multiple)
    {
        if (multiple <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiple));

        return multiple switch
        {
            1 => new TerminalMutationAssertionExpression<double, bool>(x =>
            {
                x = Math.Abs(x);
                return Math.Abs(x - Math.Round(x)) <= AssertionExpressionExtensions.RoundTolarance;
            }),
            2 => Even,
            _ => new TerminalMutationAssertionExpression<double, bool>(x =>
            {
                x = Math.Abs(x);
                double rnd = Math.Round(x);
                if (Math.Abs(x - rnd) > AssertionExpressionExtensions.RoundTolarance) return false;

                double rem = rnd % multiple;
                return rem < 0.5 || rem > multiple - 0.5;
            })
        };
    }

    /// <summary>
    /// Expression that is met when all elements in a collection meet the expression given in <paramref name="expression"/>.
    /// </summary>
    /// <remarks>Empty and null collections meet this expression.</remarks>
    public static ITerminalAssertionExpression<IEnumerable<TElementType>, bool> All<TElementType>(ITerminalAssertionExpression<TElementType, bool> expression)
    {
        return new AllAssertionExpression<TElementType> { Expression = expression };
    }

    /// <summary>
    /// Expression that is met when at least one element in a collection meets the expression given in <paramref name="expression"/>.
    /// </summary>
    /// <remarks>Empty or null collections do not meet this expression.</remarks>
    public static ITerminalAssertionExpression<IEnumerable<TElementType>, bool> Any<TElementType>(ITerminalAssertionExpression<TElementType, bool> expression)
    {
        return new AnyAssertionExpression<TElementType> { Expression = expression };
    }

    /// <summary>
    /// Expression that is met when at least <paramref name="count"/> elements in a collection meets the expression given in <paramref name="expression"/>.
    /// </summary>
    /// <remarks>Empty or null collections do not meet this expression.</remarks>
    public static ITerminalAssertionExpression<IEnumerable<TElementType>, bool> AtLeast<TElementType>(ulong count, ITerminalAssertionExpression<TElementType, bool> expression)
    {
        return new AtLeastAssertionExpression<TElementType>(count) { Expression = expression };
    }

    /// <summary>
    /// Expression that is met when no more than <paramref name="count"/> elements in a collection meets the expression given in <paramref name="expression"/>.
    /// </summary>
    /// <remarks>Empty and null collections meet this expression.</remarks>
    public static ITerminalAssertionExpression<IEnumerable<TElementType>, bool> AtMost<TElementType>(ulong count, ITerminalAssertionExpression<TElementType, bool> expression)
    {
        return new AtMostAssertionExpression<TElementType>(count) { Expression = expression };
    }

    /// <summary>
    /// Expression that is met when a collection or string is empty.
    /// </summary>
    /// <remarks>Null collections do not count as empty.</remarks>
    public static ITerminalAssertionExpression<IEnumerable, bool> Empty => new TerminalMutationAssertionExpression<IEnumerable, bool>(x =>
    {
        if (x == null)
            return false;

        if (x is string str)
            return str.Length == 0;

        if (x is ICollection c)
            return c.Count == 0;

        IEnumerator enumerator = x.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            if (enumerator is IDisposable disp)
                disp.Dispose();
        }
    });

    /// <summary>
    /// Expression that is met when a string is white-space or empty.
    /// </summary>
    /// <remarks>Null strings do not count as empty.</remarks>
    public static ITerminalAssertionExpression<string, bool> WhiteSpace => new TerminalMutationAssertionExpression<string, bool>(s => s != null && string.IsNullOrWhiteSpace(s));

    /// <summary>
    /// Expression that is met when a collection or string contains all unique values.
    /// </summary>
    public static ITerminalAssertionExpression<IEnumerable<TElementType>, bool> Unique<TElementType>()
    {
        return new UniqueAssertionExpression<TElementType>();
    }

    // chosen to leave out XmlSerializable cause it's kinda useless for this library

    /// <summary>
    /// Expression that is met when the two values are equal.
    /// </summary>
    /// <remarks>For collections, use <see cref="EquivalentTo{T}(IEnumerable{T})"/></remarks>
    public static IEqualToAssertionExpression<T> EqualTo<T>(T? expected)
    {
        return new EqualToAssertionExpression<T>(expected);
    }

    /// <summary>
    /// Expression that is met when the two values are equal. Use the generic overload to specify tolerance.
    /// </summary>
    /// <remarks>For collections, use <see cref="EquivalentTo(IEnumerable)"/></remarks>
    public static ITerminalAssertionExpression<object, bool> EqualTo(object? expected)
    {
        if (expected == null)
            return Null;

        return new TerminalMutationAssertionExpression<object, bool>(v => EqualityHelper.ValueEquals(v, expected));
    }

    /// <summary>
    /// Expression that is met when the two values are equal by reference.
    /// </summary>
    public static ITerminalAssertionExpression<T, bool> SameAs<T>(T? expected) where T : class
    {
        return new TerminalMutationAssertionExpression<T, bool>(v => ReferenceEquals(v, expected));
    }

    /// <summary>
    /// Expression that is met when the two values are equal by reference.
    /// </summary>
    public static ITerminalAssertionExpression<object, bool> SameAs(object? expected)
    {
        if (expected == null)
            return Null;

        return new TerminalMutationAssertionExpression<object, bool>(v => ReferenceEquals(v, expected));
    }

    /// <summary>
    /// Expression that is met when a value is greater than the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> GreaterThan<T>(T? other) where T : IComparable<T>
    {
        return new CompareAssertionExpression<T>(other, 2);
    }

    /// <summary>
    /// Expression that is met when a value is greater than or equal to the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> GreaterThanOrEqualTo<T>(T? other) where T : IComparable<T>
    {
        return new CompareAssertionExpression<T>(other, 1);
    }

    /// <summary>
    /// Expression that is met when a value is greater than or equal to the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> AtLeast<T>(T? other) where T : IComparable<T>
    {
        return GreaterThanOrEqualTo(other);
    }

    /// <summary>
    /// Expression that is met when a value is less than the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> LessThan<T>(T? other) where T : IComparable<T>
    {
        return new CompareAssertionExpression<T>(other, -2);
    }

    /// <summary>
    /// Expression that is met when a value is less than or equal to the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> LessThanOrEqualTo<T>(T? other) where T : IComparable<T>
    {
        return new CompareAssertionExpression<T>(other, -1);
    }

    /// <summary>
    /// Expression that is met when a value is less than or equal to the actual value.
    /// </summary>
    public static ICompareAssertionExpression<T> AtMost<T>(T? other) where T : IComparable<T>
    {
        return LessThanOrEqualTo(other);
    }

    /// <summary>
    /// Expression that is met when the actual value is the exact same type as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>Use <see cref="InstanceOf"/> if you need to see if it's of the same type or derived from it.</remarks>
    public static ITerminalAssertionExpression<object, bool> TypeOf<T>()
    {
        return TypeOf(typeof(T));
    }

    /// <summary>
    /// Expression that is met when the actual value is the exact same type as <paramref name="type"/>.
    /// </summary>
    /// <remarks>Use <see cref="InstanceOf"/> if you need to see if it's of the same type or derived from it.</remarks>
    public static ITerminalAssertionExpression<object, bool> TypeOf(Type type)
    {
        return new TerminalMutationAssertionExpression<object, bool>(v => v != null && v.GetType() == type);
    }

    /// <summary>
    /// Expression that is met when the actual value is an instance of <typeparamref name="T"/>. Note this doesn't include interfaces.
    /// </summary>
    /// <remarks>Use <see cref="InstanceOf"/> if you need to see if it's of the same type or derived from it.</remarks>
    public static ITerminalAssertionExpression<object, bool> InstanceOf<T>()
    {
        return InstanceOf(typeof(T));
    }

    /// <summary>
    /// Expression that is met when the actual value is an instance of <paramref name="type"/> or implements it as an interface.
    /// </summary>
    /// <remarks>Use <see cref="InstanceOf"/> if you need to see if it's of the same type or derived from it.</remarks>
    public static ITerminalAssertionExpression<object, bool> InstanceOf(Type type)
    {
        return new TerminalMutationAssertionExpression<object, bool>(v => v != null && type.IsInstanceOfType(v));
    }

    /// <summary>
    /// Expression that is met when the actual value is a type that is assignable from a value of <typeparamref name="T"/>.
    /// </summary>
    public static ITerminalAssertionExpression<Type, bool> AssignableFrom<T>()
    {
        return AssignableFrom(typeof(T));
    }

    /// <summary>
    /// Expression that is met when the actual value is a type that is assignable from a value of <paramref name="type"/>.
    /// </summary>
    public static ITerminalAssertionExpression<Type, bool> AssignableFrom(Type type)
    {
        return new TerminalMutationAssertionExpression<Type, bool>(v => v != null && v.IsAssignableFrom(type));
    }

    /// <summary>
    /// Expression that is met when the actual value is a type that is assignable from a value of <typeparamref name="T"/>.
    /// </summary>
    public static ITerminalAssertionExpression<Type, bool> AssignableTo<T>()
    {
        return AssignableTo(typeof(T));
    }

    /// <summary>
    /// Expression that is met when the actual value is a type that is assignable from a value of <paramref name="type"/>.
    /// </summary>
    public static ITerminalAssertionExpression<Type, bool> AssignableTo(Type type)
    {
        return new TerminalMutationAssertionExpression<Type, bool>(v => type?.IsAssignableFrom(v) ?? false);
    }

    /// <summary>
    /// Expression that is met when two enumerables are structurally equal.
    /// </summary>
    public static ITerminalAssertionExpression<IEnumerable<T>, bool> EquivalentTo<T>(IEnumerable<T> enumerable)
    {
        return new EquivalentCollectionAssertionExpression<T>(enumerable);
    }

    /// <summary>
    /// Expression that is met when all elements of <paramref name="enumerable"/> are present in the actual value.
    /// </summary>
    public static ITerminalAssertionExpression<IEnumerable<T>, bool> SubsetOf<T>(IEnumerable<T> enumerable)
    {
        return new SubsetOrSupersetCollectionAssertionExpression<T>(enumerable, isSuperset: false);
    }

    /// <summary>
    /// Expression that is met when all elements of the actual value are present in <paramref name="enumerable"/>.
    /// </summary>
    public static ITerminalAssertionExpression<IEnumerable<T>, bool> SupersetOf<T>(IEnumerable<T> enumerable)
    {
        return new SubsetOrSupersetCollectionAssertionExpression<T>(enumerable, isSuperset: true);
    }

    /// <summary>
    /// Expression that is met if the collection is in (by default, ascending) order.
    /// </summary>
    public static IOrderedCollectionTerminalAssertionExpression<T> Ordered<T>()
    {
        return new OrderedCollectionAssertionExpression<T>(descending: false);
    }
}

public static class AssertionExpressionExtensions
{
    public const double RoundTolarance = 0.0001;
    
    extension<T>(INonTerminalAssertionExpression<bool, T> booleanExpression)
    {
        /// <inheritdoc cref="Is.Not"/>
        public INonTerminalAssertionExpression<bool, bool> Not => Is.Not.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Null"/>
        public ITerminalAssertionExpression<object, bool> Null => Is.Null.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Default"/>
        public ITerminalAssertionExpression<object, bool> Default => Is.Default.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.True"/>
        public ITerminalAssertionExpression<bool, bool> True => Is.True.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.False"/>
        public ITerminalAssertionExpression<bool, bool> False => Is.False.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Zero"/>
        public ITerminalAssertionExpression<double, bool> Zero => Is.Zero.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.NaN"/>
        public ITerminalAssertionExpression<double, bool> NaN => Is.NaN.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Infinity"/>
        public ITerminalAssertionExpression<double, bool> Infinity => Is.Infinity.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Finite"/>
        public ITerminalAssertionExpression<double, bool> Finite => Is.Finite.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Positive"/>
        public ISignAssertionExpression<double> Positive => Is.Positive.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Negative"/>
        public ISignAssertionExpression<double> Negative => Is.Negative.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Odd"/>
        public ITerminalAssertionExpression<double, bool> Odd => Is.Odd.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Even"/>
        public ITerminalAssertionExpression<double, bool> Even => Is.Even.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.MultipleOf"/>
        public ITerminalAssertionExpression<double, bool> MultipleOf(int multiple) => Is.MultipleOf(multiple).SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.All{TElementType}"/>
        public ITerminalAssertionExpression<IEnumerable<TElementType>, bool> All<TElementType>(ITerminalAssertionExpression<TElementType, bool> expression)
        {
            return Is.All(expression).SetParentOf(booleanExpression);
        }

        /// <inheritdoc cref="Is.Any{TElementType}"/>
        public ITerminalAssertionExpression<IEnumerable<TElementType>, bool> Any<TElementType>(ITerminalAssertionExpression<TElementType, bool> expression)
        {
            return Is.Any(expression).SetParentOf(booleanExpression);
        }

        /// <inheritdoc cref="Is.AtLeast{TElementType}(ulong, ITerminalAssertionExpression{TElementType, bool})"/>
        public ITerminalAssertionExpression<IEnumerable<TElementType>, bool> AtLeast<TElementType>(ulong count, ITerminalAssertionExpression<TElementType, bool> expression)
        {
            return Is.AtLeast(count, expression).SetParentOf(booleanExpression);
        }

        /// <inheritdoc cref="Is.AtMost{TElementType}(ulong, ITerminalAssertionExpression{TElementType, bool})"/>
        public ITerminalAssertionExpression<IEnumerable<TElementType>, bool> AtMost<TElementType>(ulong count, ITerminalAssertionExpression<TElementType, bool> expression)
        {
            return Is.AtMost(count, expression).SetParentOf(booleanExpression);
        }

        /// <inheritdoc cref="Is.Empty"/>
        public ITerminalAssertionExpression<IEnumerable, bool> Empty => Is.Empty.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.WhiteSpace"/>
        public ITerminalAssertionExpression<string, bool> WhiteSpace => Is.WhiteSpace.SetParentOf(booleanExpression);

        /// <inheritdoc cref="Is.Unique{TElementType}"/>
        public ITerminalAssertionExpression<IEnumerable<TElementType>, bool> Unique<TElementType>() => Is.Unique<TElementType>().SetParentOf(booleanExpression);

    }

    extension(INonTerminalAssertionExpression<decimal, decimal> decimalExpression)
    {
        // dont need SetParentOf since Is.Decimal is a singleton.

        /// <inheritdoc cref="Is.Positive"/>
        public ISignAssertionExpression<decimal> Positive => decimalExpression != Is.Decimal
            ? throw ThrowInvalidCombination()
            : new DecimalSignAssertionExpression(1) { SpecialBehavior = SpecialBehavior.Positive };

        /// <inheritdoc cref="Is.Negative"/>
        public ISignAssertionExpression<decimal> Negative => decimalExpression != Is.Decimal
            ? throw ThrowInvalidCombination()
            : new DecimalSignAssertionExpression(-1) { SpecialBehavior = SpecialBehavior.Negative };

        /// <inheritdoc cref="Is.Zero"/>
        public ITerminalAssertionExpression<decimal, bool> Zero => decimalExpression != Is.Decimal
            ? throw ThrowInvalidCombination()
            : new TerminalMutationAssertionExpression<decimal, bool>(x => x == 0);
    }

    extension(ISignAssertionExpression<double> signExpressions)
    {
        public ITerminalAssertionExpression<double, bool> Infinity
        {
            get
            {
                return GetSpecialBehavior(signExpressions) switch
                {
                    SpecialBehavior.Positive => Replace(signExpressions, new TerminalMutationAssertionExpression<double, bool>(double.IsPositiveInfinity)),
                    SpecialBehavior.Negative => Replace(signExpressions, new TerminalMutationAssertionExpression<double, bool>(double.IsNegativeInfinity)),
                    _ => Replace(signExpressions, Is.Infinity)
                };
            }
        }

        public ITerminalAssertionExpression<double, bool> Zero
        {
            get
            {
                return GetSpecialBehavior(signExpressions) switch
                {
                    SpecialBehavior.Positive => Replace(signExpressions, new TerminalMutationAssertionExpression<double, bool>(x => x != -0.0 && x == 0.0)),
                    SpecialBehavior.Negative => Replace(signExpressions, new TerminalMutationAssertionExpression<double, bool>(x => x == -0.0)),
                    _ => Replace(signExpressions, new TerminalMutationAssertionExpression<double, bool>(x => x == 0))
                };
            }
        }
    }

    extension(ICompareAssertionExpression<byte> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<byte> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<byte> expr = (ICompareAssertionExpressionWithTolerance<byte>)equalTo;
            expr.SetTolerance(new Tolerance<int>(Math.Abs(tolerance)));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<sbyte> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<sbyte> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<sbyte> expr = (ICompareAssertionExpressionWithTolerance<sbyte>)equalTo;
            expr.SetTolerance(new Tolerance<int>(Math.Abs(tolerance)));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<short> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<short> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<short> expr = (ICompareAssertionExpressionWithTolerance<short>)equalTo;
            expr.SetTolerance(new Tolerance<int>(Math.Abs(tolerance)));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<ushort> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<ushort> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<ushort> expr = (ICompareAssertionExpressionWithTolerance<ushort>)equalTo;
            expr.SetTolerance(new Tolerance<int>(Math.Abs(tolerance)));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<int> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<int> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<int> expr = (ICompareAssertionExpressionWithTolerance<int>)equalTo;
            expr.SetTolerance(new Tolerance<int>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<int> Within(uint tolerance)
        {
            ICompareAssertionExpressionWithTolerance<int> expr = (ICompareAssertionExpressionWithTolerance<int>)equalTo;
            expr.SetTolerance(new Tolerance<uint>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<uint> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<uint> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<uint> expr = (ICompareAssertionExpressionWithTolerance<uint>)equalTo;
            expr.SetTolerance(new Tolerance<int>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<uint> Within(uint tolerance)
        {
            ICompareAssertionExpressionWithTolerance<uint> expr = (ICompareAssertionExpressionWithTolerance<uint>)equalTo;
            expr.SetTolerance(new Tolerance<uint>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<long> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<long> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<long> expr = (ICompareAssertionExpressionWithTolerance<long>)equalTo;
            expr.SetTolerance(new Tolerance<int>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<long> Within(ulong tolerance)
        {
            ICompareAssertionExpressionWithTolerance<long> expr = (ICompareAssertionExpressionWithTolerance<long>)equalTo;
            expr.SetTolerance(new Tolerance<ulong>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<ulong> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<ulong> Within(int tolerance)
        {
            ICompareAssertionExpressionWithTolerance<ulong> expr = (ICompareAssertionExpressionWithTolerance<ulong>)equalTo;
            expr.SetTolerance(new Tolerance<int>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<ulong> Within(ulong tolerance)
        {
            ICompareAssertionExpressionWithTolerance<ulong> expr = (ICompareAssertionExpressionWithTolerance<ulong>)equalTo;
            expr.SetTolerance(new Tolerance<ulong>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<double> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<double> Within(double tolerance)
        {
            ICompareAssertionExpressionWithTolerance<double> expr = (ICompareAssertionExpressionWithTolerance<double>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<double> Within(decimal tolerance)
        {
            ICompareAssertionExpressionWithTolerance<double> expr = (ICompareAssertionExpressionWithTolerance<double>)equalTo;
            expr.SetTolerance(new Tolerance<decimal>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<float> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<float> Within(double tolerance)
        {
            ICompareAssertionExpressionWithTolerance<float> expr = (ICompareAssertionExpressionWithTolerance<float>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<float> Within(decimal tolerance)
        {
            ICompareAssertionExpressionWithTolerance<float> expr = (ICompareAssertionExpressionWithTolerance<float>)equalTo;
            expr.SetTolerance(new Tolerance<decimal>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<decimal> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<decimal> Within(double tolerance)
        {
            ICompareAssertionExpressionWithTolerance<decimal> expr = (ICompareAssertionExpressionWithTolerance<decimal>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<decimal> Within(decimal tolerance)
        {
            ICompareAssertionExpressionWithTolerance<decimal> expr = (ICompareAssertionExpressionWithTolerance<decimal>)equalTo;
            expr.SetTolerance(new Tolerance<decimal>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<BigInteger> equalTo)
    {
        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<BigInteger> Within(ulong tolerance)
        {
            ICompareAssertionExpressionWithTolerance<BigInteger> expr = (ICompareAssertionExpressionWithTolerance<BigInteger>)equalTo;
            expr.SetTolerance(new Tolerance<ulong>(tolerance));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<BigInteger> Within(BigInteger tolerance)
        {
            ICompareAssertionExpressionWithTolerance<BigInteger> expr = (ICompareAssertionExpressionWithTolerance<BigInteger>)equalTo;
            expr.SetTolerance(new Tolerance<BigInteger>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<TimeSpan> equalTo)
    {
        /// <summary>
        /// Checks if this <see cref="TimeSpan"/> is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpression<TimeSpan> Within(TimeSpan tolerance)
        {
            ICompareAssertionExpressionWithTolerance<TimeSpan> expr = (ICompareAssertionExpressionWithTolerance<TimeSpan>)equalTo;
            expr.SetTolerance(new Tolerance<long>(tolerance.Ticks));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in ticks, or this function call can be followed by a time unit to use that unit.
        /// <para><code>Assert.That(someValue, Is.EqualTo(TimeSpan.FromSeconds(30)).Within(10).Seconds);</code></para>
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Within(double tolerance)
        {
            ICompareAssertionExpressionWithTolerance<TimeSpan> expr = (ICompareAssertionExpressionWithTolerance<TimeSpan>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<DateTime> equalTo)
    {
        /// <summary>
        /// Checks if this <see cref="TimeSpan"/> is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpression<DateTime> Within(TimeSpan tolerance)
        {
            ICompareAssertionExpressionWithTolerance<DateTime> expr = (ICompareAssertionExpressionWithTolerance<DateTime>)equalTo;
            expr.SetTolerance(new Tolerance<long>(tolerance.Ticks));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in ticks, or this function call can be followed by a time unit to use that unit.
        /// <para><code>Assert.That(someValue, Is.EqualTo(DateTime.Now).Within(10).Seconds);</code></para>
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Within(double tolerance)
        {
            ICompareAssertionExpressionWithTolerance<DateTime> expr = (ICompareAssertionExpressionWithTolerance<DateTime>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpression<DateTimeOffset> equalTo)
    {
        /// <summary>
        /// Checks if this <see cref="TimeSpan"/> is within the given <paramref name="tolerance"/> in both directions.
        /// </summary>
        public ICompareAssertionExpression<DateTimeOffset> Within(TimeSpan tolerance)
        {
            if (HasCustomComparer(equalTo))
                throw new InvalidOperationException(Properties.Resources.AssertionExpressionCombinedDateTimeOffsetComparisonWithTolerance);

            ICompareAssertionExpressionWithTolerance<DateTimeOffset> expr = (ICompareAssertionExpressionWithTolerance<DateTimeOffset>)equalTo;
            expr.SetTolerance(new Tolerance<long>(tolerance.Ticks));
            return expr;
        }

        /// <summary>
        /// Checks if this value is within the given <paramref name="tolerance"/> in ticks, or this function call can be followed by a time unit to use that unit.
        /// <para><code>Assert.That(someValue, Is.EqualTo(DateTimeOffset.Now).Within(10).Seconds);</code></para>
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Within(double tolerance)
        {
            if (HasCustomComparer(equalTo))
                throw new InvalidOperationException(Properties.Resources.AssertionExpressionCombinedDateTimeOffsetComparisonWithTolerance);

            ICompareAssertionExpressionWithTolerance<DateTimeOffset> expr = (ICompareAssertionExpressionWithTolerance<DateTimeOffset>)equalTo;
            expr.SetTolerance(new Tolerance<double>(tolerance));
            return expr;
        }
    }

    extension(ICompareAssertionExpressionWithTolerance<TimeSpan> equalToWithin)
    {
        /// <summary>
        /// Specifies that the tolerance specified is in ticks.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Ticks => TimeTicks(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in milliseconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Milliseconds => TimeMilliseconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in seconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Seconds => TimeSeconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in minutes.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Minutes => TimeMinutes(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in hours.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Hours => TimeHours(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in days.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<TimeSpan> Days => TimeDays(equalToWithin);
    }

    extension(ICompareAssertionExpressionWithTolerance<DateTime> equalToWithin)
    {
        /// <summary>
        /// Specifies that the tolerance specified is in ticks.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Ticks => TimeTicks(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in milliseconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Milliseconds => TimeMilliseconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in seconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Seconds => TimeSeconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in minutes.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Minutes => TimeMinutes(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in hours.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Hours => TimeHours(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in days.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTime> Days => TimeDays(equalToWithin);
    }

    extension(ICompareAssertionExpressionWithTolerance<DateTimeOffset> equalToWithin)
    {
        /// <summary>
        /// Specifies that the tolerance specified is in ticks.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Ticks => TimeTicks(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in milliseconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Milliseconds => TimeMilliseconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in seconds.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Seconds => TimeSeconds(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in minutes.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Minutes => TimeMinutes(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in hours.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Hours => TimeHours(equalToWithin);

        /// <summary>
        /// Specifies that the tolerance specified is in days.
        /// </summary>
        public ICompareAssertionExpressionWithTolerance<DateTimeOffset> Days => TimeDays(equalToWithin);
    }

    extension(IEqualToAssertionExpression<DateTimeOffset> equalToWithin)
    {
        /// <summary>
        /// Specifies that the offset should be included in the comparison.
        /// </summary>
        /// <remarks>Tolerances are ignored when this property is used.</remarks>
        public IEqualToAssertionExpression<DateTimeOffset> WithSameOffset
        {
            get
            {
                if (equalToWithin is EqualToAssertionExpression<DateTimeOffset> impl)
                {
                    if (impl.Tolerance != null)
                        throw new InvalidOperationException(Properties.Resources.AssertionExpressionCombinedDateTimeOffsetComparisonWithTolerance);
                    impl.Comparer = DateTimeOffsetFullComparer.Instance;
                }
                else
                {
                    ThrowInvalidCombination();
                }

                return equalToWithin;
            }
        }
    }

    extension(IEqualToAssertionExpression<string> equalTo)
    {
        /// <summary>
        /// Specifies that the string comparison should use <paramref name="comparer"/> to compare them.
        /// </summary>
        public IEqualToAssertionExpression<string> Using(StringComparer comparer)
        {
            if (equalTo is EqualToAssertionExpression<string> impl)
            {
                impl.Comparer = comparer;
            }
            else
            {
                ThrowInvalidCombination();
            }

            return equalTo;
        }
    }

    private static bool HasCustomComparer<T>(ICompareAssertionExpression<T> equalTo)
    {
        return equalTo is EqualToAssertionExpression<T> { Comparer: not null };
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeTicks<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        if (equalToWithin.Tolerance is Tolerance<long> tickTolerance)
        {
            tickTolerance.Unit = ToleranceUnit.Linear;
        }
        else
        {
            ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x, ToleranceUnit.Linear);
            equalToWithin.Tolerance!.VisitTolerance(ref visitor);
        }

        return equalToWithin;
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeMilliseconds<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x * TimeSpan.TicksPerMillisecond, ToleranceUnit.Linear);
        equalToWithin.Tolerance!.VisitTolerance(ref visitor);

        return equalToWithin;
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeSeconds<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x * TimeSpan.TicksPerSecond, ToleranceUnit.Linear);
        equalToWithin.Tolerance!.VisitTolerance(ref visitor);

        return equalToWithin;
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeMinutes<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x * TimeSpan.TicksPerMinute, ToleranceUnit.Linear);
        equalToWithin.Tolerance!.VisitTolerance(ref visitor);

        return equalToWithin;
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeHours<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x * TimeSpan.TicksPerHour, ToleranceUnit.Linear);
        equalToWithin.Tolerance!.VisitTolerance(ref visitor);

        return equalToWithin;
    }

    private static ICompareAssertionExpressionWithTolerance<T> TimeDays<T>(ICompareAssertionExpressionWithTolerance<T> equalToWithin)
    {
        AssertHasTolerance(equalToWithin);

        ToleranceMutateValueVisitor<double> visitor = new ToleranceMutateValueVisitor<double>(x => x * TimeSpan.TicksPerDay, ToleranceUnit.Linear);
        equalToWithin.Tolerance!.VisitTolerance(ref visitor);

        return equalToWithin;
    }

    private readonly struct ToleranceMutateValueVisitor<TValue>(Func<TValue, TValue> value, ToleranceUnit unit = ToleranceUnit.Linear)
        : IToleranceVisitor
        where TValue : IComparable<TValue>, IEquatable<TValue>
    {
        private readonly Func<TValue, TValue> _value = value;
        private readonly ToleranceUnit _unit = unit;

        /// <inheritdoc />
        public void Visit<TValueType>(Tolerance<TValueType> tolerance) where TValueType : IComparable<TValueType>, IEquatable<TValueType>
        {
            if (tolerance is Tolerance<TValue> tol)
            {
                tol.Value = _value(tol.Value);
                tol.Unit = _unit;
            }
            else
            {
                ThrowInvalidCombination();
            }
        }
    }

    internal static void AssertHasTolerance<T>(ICompareAssertionExpressionWithTolerance<T> t)
    {
        if (t.Tolerance == null)
            throw new InvalidOperationException(Properties.Resources.AssertionExpressionInvalidCombination);
    }
    internal static InvalidOperationException ThrowInvalidCombination()
    {
        return new InvalidOperationException(Properties.Resources.AssertionExpressionInvalidCombination);
    }

    private static TExpr SetParentOf<TExpr, TIn, TOut>(this TExpr parent, INonTerminalAssertionExpression<TIn, TOut> child) where TExpr : class, IAssertionExpressionParent<TIn>
    {
        child.Parent = parent;
        parent.Child = child;
        return parent;
    }

    private static SpecialBehavior GetSpecialBehavior(IAssertionExpression expr)
    {
        return expr is ISpecialBehaviorAssertionExpression sp ? sp.SpecialBehavior : SpecialBehavior.None;
    }

    private static TExpr Replace<TExpr, TOldIn, TOut>(IAssertionExpression<TOldIn, TOut> old, TExpr @new) where TExpr : class, IAssertionExpressionParent<TOut>
    {
        // Is.Not.Positive.Infinity;
        IAssertionExpression? oldChild = old.Child;
        if (oldChild != null)
        {
            if (oldChild is not IAssertionExpressionChild<TOut> oldChildTyped)
            {
                throw ThrowInvalidCombination();
            }

            oldChildTyped.Parent = @new;
            @new.Child = oldChildTyped;
            old.Child = null;
        }

        if (old is IDisposable disp)
            disp.Dispose();

        return @new;
    }

}