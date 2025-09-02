using System.Numerics;
using System.Runtime.CompilerServices;

namespace uTest.Runner.Util;

internal static class RangeHelper
{
    /// <summary>
    /// Maximum number of variations a test can have.
    /// </summary>
    public const int MaxTestVariations = 16384;

    /// <summary>
    /// Gets all values present in a range.
    /// </summary>
    public static Array GetRangeValues(IUnturnedTestRangeParameter parameter)
    {
        RangeVisitor visitor = default;
        parameter.Visit(ref visitor);
        return visitor.Range ?? throw new InvalidOperationException();
    }

    /// <summary>
    /// Calculates the number of values present in a range.
    /// </summary>
    public static ulong GetRangeValueCount(IUnturnedTestRangeParameter parameter)
    {
        CountVisitor visitor = default;
        parameter.Visit(ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Calculates the number of values present in a range.
    /// </summary>
    public static ulong GetRangeValueCount<T, TStep>(T from, T to, TStep step)
        where T : unmanaged
        where TStep : unmanaged, IComparable<TStep>
    {
        return CleanupRangeParams(ref from, ref to, ref step)
            ? GetRangeValueCountIntl(from, to, step)
            : 1ul;
    }

    /// <summary>
    /// Gets all values present in a range.
    /// </summary>
    public static T[] GetRangeValues<T, TStep>(T from, T to, TStep step)
        where T : unmanaged
        where TStep : unmanaged, IComparable<TStep>
    {
        if (!CleanupRangeParams(ref from, ref to, ref step))
        {
            return [ from ];
        }

        // assume: step > 0
        // assume: from < to

        ulong ct = GetRangeValueCountIntl(from, to, step);
        switch (ct)
        {
            case <= 0 or > MaxTestVariations:
                return Array.Empty<T>();
            case 1: return [ from ];
        }

        T[] array = new T[ct];
        ulong index = 0;

        do
        {
            array[index] = from;
            ++index;
            Increment(ref from, step);
        }
        while (index < ct);
        
        return array;
    }

    private static void Increment<T, TStep>(ref T value, TStep step)
        where T : unmanaged
        where TStep : unmanaged
    {
        if (typeof(T).IsEnum)
        {
            Type underlyingType = typeof(T).GetEnumUnderlyingType();
            if (underlyingType == typeof(bool))
                value = As<bool, T>(!As<T, bool>(value));
            else if (underlyingType == typeof(byte))
                value = As<byte, T>((byte)(As<T, byte>(value) + 1));
            else if(underlyingType == typeof(sbyte))
                value = As<sbyte, T>((sbyte)(As<T, sbyte>(value) + 1));
            else if(underlyingType == typeof(ushort))
                value = As<ushort, T>((ushort)(As<T, ushort>(value) + 1));
            else if(underlyingType == typeof(char))
                value = As<char, T>((char)(As<T, char>(value) + 1));
            else if(underlyingType == typeof(short))
                value = As<short, T>((short)(As<T, short>(value) + 1));
            else if (underlyingType == typeof(uint))
                value = As<uint, T>(As<T, uint>(value) + 1u);
            else if (underlyingType == typeof(int))
                value = As<int, T>(As<T, int>(value) + 1);
            else if (underlyingType == typeof(ulong))
                value = As<ulong, T>(As<T, ulong>(value) + 1ul);
            else if (underlyingType == typeof(long))
                value = As<long, T>(As<T, long>(value) + 1L);
            return;
        }

        if (typeof(T) == typeof(bool))
            value = As<bool, T>(!As<T, bool>(value));
        else if (typeof(T) == typeof(byte))
            value = As<byte, T>((byte)(As<T, byte>(value) + As<TStep, byte>(step)));
        else if (typeof(T) == typeof(sbyte))
            value = As<sbyte, T>((sbyte)(As<T, sbyte>(value) + As<TStep, sbyte>(step)));
        else if (typeof(T) == typeof(ushort))
            value = As<ushort, T>((ushort)(As<T, ushort>(value) + As<TStep, ushort>(step)));
        else if (typeof(T) == typeof(char))
            value = As<char, T>((char)(As<T, char>(value) + As<TStep, int>(step)));
        else if (typeof(T) == typeof(short))
            value = As<short, T>((short)(As<T, short>(value) + As<TStep, short>(step)));
        else if (typeof(T) == typeof(uint))
            value = As<uint, T>(As<T, uint>(value) + As<TStep, uint>(step));
        else if (typeof(T) == typeof(int))
            value = As<int, T>(As<T, int>(value) + As<TStep, int>(step));
        else if (typeof(T) == typeof(ulong))
            value = As<ulong, T>(As<T, ulong>(value) + As<TStep, ulong>(step));
        else if (typeof(T) == typeof(long))
            value = As<long, T>(As<T, long>(value) + As<TStep, long>(step));
        else if (typeof(T) == typeof(double))
            value = As<double, T>(As<T, double>(value) + As<TStep, double>(step));
        else if (typeof(T) == typeof(float))
            value = As<float, T>(As<T, float>(value) + As<TStep, float>(step));
        else if (typeof(T) == typeof(decimal))
            value = As<decimal, T>(As<T, decimal>(value) + As<TStep, decimal>(step));
        else if (typeof(T) == typeof(BigInteger))
            value = As<BigInteger, T>(As<T, BigInteger>(value) + As<TStep, BigInteger>(step));
    }

    private static ulong GetRangeValueCountIntl<T, TStep>(T from, T to, TStep step)
        where T : unmanaged
        where TStep : unmanaged
    {
        if (typeof(T).IsEnum)
        {
            Type underlyingType = typeof(T).GetEnumUnderlyingType();
            if (underlyingType == typeof(bool))
                return As<T, bool>(from) == As<T, bool>(to) ? 1ul : 2ul;

            if (underlyingType == typeof(byte))
                return (ulong)(As<T, byte>(to) - As<T, byte>(from) + 1);
            
            if (underlyingType == typeof(sbyte))
                return (ulong)(As<T, sbyte>(to) - As<T, sbyte>(from) + 1);
            
            if (underlyingType == typeof(ushort))
                return (ulong)(As<T, ushort>(to) - As<T, ushort>(from) + 1);
            
            if (underlyingType == typeof(char))
                return (ulong)(As<T, char>(to) - As<T, char>(from) + 1);
            
            if (underlyingType == typeof(short))
                return (ulong)(As<T, short>(to) - As<T, short>(from) + 1);
            
            if (underlyingType == typeof(uint))
                return As<T, uint>(to) - As<T, uint>(from) + 1u;
            
            if (underlyingType == typeof(int))
                return (ulong)((long)As<T, int>(to) - As<T, int>(from) + 1);
            
            if (underlyingType == typeof(ulong))
                return As<T, ulong>(to) - As<T, ulong>(from) + 1ul;
            
            if (underlyingType == typeof(long))
                return (ulong)(As<T, long>(to) - As<T, long>(from)) + 1;

            return 1;
        }

        if (typeof(T) == typeof(bool))
            return As<T, bool>(from) == As<T, bool>(to) ? 1ul : 2ul;
        
        if (typeof(T) == typeof(byte))
            return (ulong)((As<T, byte>(to) - As<T, byte>(from)) / As<TStep, byte>(step) + 1);
        
        if (typeof(T) == typeof(sbyte))
            return (ulong)((As<T, sbyte>(to) - As<T, sbyte>(from)) / As<TStep, sbyte>(step) + 1);
        
        if (typeof(T) == typeof(ushort))
            return (ulong)((As<T, ushort>(to) - As<T, ushort>(from)) / As<TStep, ushort>(step) + 1);
        
        if (typeof(T) == typeof(char))
            return (ulong)((As<T, char>(to) - As<T, char>(from)) / As<TStep, int>(step) + 1);
        
        if (typeof(T) == typeof(short))
            return (ulong)((As<T, short>(to) - As<T, short>(from)) / As<TStep, short>(step) + 1);
        
        if (typeof(T) == typeof(uint))
            return (As<T, uint>(to) - As<T, uint>(from)) / As<TStep, uint>(step) + 1u;
        
        if (typeof(T) == typeof(int))
            return (ulong)(((long)As<T, int>(to) - As<T, int>(from)) / As<TStep, int>(step) + 1);
        
        if (typeof(T) == typeof(ulong))
            return (As<T, ulong>(to) - As<T, ulong>(from)) / As<TStep, ulong>(step) + 1ul;
        
        if (typeof(T) == typeof(long))
            return (ulong)(As<T, long>(to) - As<T, long>(from)) / (ulong)As<TStep, long>(step) + 1;
        
        if (typeof(T) == typeof(double))
            return (ulong)Math.Ceiling((As<T, double>(to) - As<T, double>(from)) / As<TStep, double>(step));

        if (typeof(T) == typeof(float))
            return (ulong)Math.Ceiling((As<T, float>(to) - As<T, float>(from)) / As<TStep, float>(step));
        
        if (typeof(T) == typeof(decimal))
            return (ulong)decimal.Ceiling((As<T, decimal>(to) - As<T, decimal>(from)) / As<TStep, decimal>(step));
        
        if (typeof(T) == typeof(BigInteger))
            return (ulong)((As<T, BigInteger>(to) - As<T, BigInteger>(from)) / As<TStep, BigInteger>(step) + 1);
        
        return 1;
    }

    private static bool CleanupRangeParams<T, TStep>(ref T from, ref T to, ref TStep step)
        where T : unmanaged
        where TStep : unmanaged, IComparable<TStep>
    {
        if (!typeof(T).IsEnum)
        {
            switch (step.CompareTo(default))
            {
                case < 0:
                    (from, to) = (to, from);
                    step = NegativeToPositive(step);
                    break;

                case 0:
                    return false;
            }
        }

        switch (Comparer<T>.Default.Compare(from, to))
        {
            case > 0:
                (from, to) = (to, from);
                break;

            case 0:
                return false;
        }

        return true;
    }

    private static TTo As<TFrom, TTo>(TFrom value)
    {
        return Unsafe.As<TFrom, TTo>(ref value);
    }

    private static T NegativeToPositive<T>(T value) where T : unmanaged
    {
        // note: can not swap sign of unsigned values
        if (typeof(T) == typeof(sbyte))
        {
            int positive = -As<T, sbyte>(value);
            if (positive > sbyte.MaxValue)
                positive = sbyte.MaxValue;
            return As<sbyte, T>((sbyte)positive);
        }
        if (typeof(T) == typeof(short))
        {
            int positive = -As<T, short>(value);
            if (positive > short.MaxValue)
                positive = short.MaxValue;
            return As<short, T>((short)positive);
        }
        if (typeof(T) == typeof(int))
        {
            int val = As<T, int>(value);
            if (val == int.MinValue)
                val = int.MinValue + 1;
            return As<int, T>(-val);
        }
        if (typeof(T) == typeof(long))
        {
            long val = As<T, long>(value);
            if (val == long.MinValue)
                val = long.MinValue + 1;
            return As<long, T>(-val);
        }
        if (typeof(T) == typeof(BigInteger))
        {
            return As<BigInteger, T>(-As<T, BigInteger>(value));
        }
        if (typeof(T) == typeof(float))
        {
            return As<float, T>(-As<T, float>(value));
        }
        if (typeof(T) == typeof(double))
        {
            return As<double, T>(-As<T, double>(value));
        }
        if (typeof(T) == typeof(decimal))
        {
            return As<decimal, T>(-As<T, decimal>(value));
        }

        return value;
    }

    private struct RangeVisitor : IUnturnedTestRangeParameterVisitor
    {
        public Array Range;

        /// <inheritdoc />
        public void Visit<T, TStep>(IUnturnedTestRangeParameter<T, TStep> parameter)
            where T : unmanaged, IConvertible
            where TStep : unmanaged, IComparable<TStep>, IConvertible
        {
            Range = GetRangeValues(parameter.From, parameter.To, parameter.Step);
        }
    }

    private struct CountVisitor : IUnturnedTestRangeParameterVisitor
    {
        public ulong Count;

        /// <inheritdoc />
        public void Visit<T, TStep>(IUnturnedTestRangeParameter<T, TStep> parameter)
            where T : unmanaged, IConvertible
            where TStep : unmanaged, IComparable<TStep>, IConvertible
        {
            Count = GetRangeValueCount(parameter.From, parameter.To, parameter.Step);
        }
    }
}
