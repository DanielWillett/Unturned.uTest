using System;
using JetBrains.Annotations;

namespace uTest;

/// <summary>
/// Defines a set of values for a parameter by using a for-loop style numeric range definition.
/// </summary>
/// <remarks>
/// Ranges are enumerated using the following for loop:
/// <code>
/// for (type value = From; value &lt;= To; value += Step);
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed class RangeAttribute : Attribute
{
    ///// <summary>
    ///// The type of data stored in this attribute.
    ///// </summary>
    //public DataType Type { get; }

    ///// <summary>
    ///// From value for <see cref="int"/> ranges.
    ///// </summary>
    //public int FromI32 { get; }

    ///// <summary>
    ///// From value for <see cref="uint"/> ranges.
    ///// </summary>
    //public uint FromU32 { get; }

    ///// <summary>
    ///// From value for <see cref="long"/> ranges.
    ///// </summary>
    //public long FromI64 { get; }

    ///// <summary>
    ///// From value for <see cref="ulong"/> ranges.
    ///// </summary>
    //public ulong FromU64 { get; }

    ///// <summary>
    ///// From value for <see cref="float"/> ranges.
    ///// </summary>
    //public float FromR32 { get; }

    ///// <summary>
    ///// From value for <see cref="double"/> ranges.
    ///// </summary>
    //public double FromR64 { get; }

    ///// <summary>
    ///// To value for <see cref="int"/> ranges.
    ///// </summary>
    //public int ToI32 { get; }

    ///// <summary>
    ///// To value for <see cref="uint"/> ranges.
    ///// </summary>
    //public uint ToU32 { get; }

    ///// <summary>
    ///// To value for <see cref="long"/> ranges.
    ///// </summary>
    //public long ToI64 { get; }

    ///// <summary>
    ///// To value for <see cref="ulong"/> ranges.
    ///// </summary>
    //public ulong ToU64 { get; }

    ///// <summary>
    ///// To value for <see cref="float"/> ranges.
    ///// </summary>
    //public float ToR32 { get; }

    ///// <summary>
    ///// To value for <see cref="double"/> ranges.
    ///// </summary>
    //public double ToR64 { get; }

    ///// <summary>
    ///// Step value for <see cref="int"/> ranges.
    ///// </summary>
    //public int StepI32 { get; }

    ///// <summary>
    ///// Step value for <see cref="uint"/> ranges.
    ///// </summary>
    //public uint StepU32 { get; }

    ///// <summary>
    ///// Step value for <see cref="long"/> ranges.
    ///// </summary>
    //public long StepI64 { get; }

    ///// <summary>
    ///// Step value for <see cref="ulong"/> ranges.
    ///// </summary>
    //public ulong StepU64 { get; }

    ///// <summary>
    ///// Step value for <see cref="float"/> ranges.
    ///// </summary>
    //public float StepR32 { get; }

    ///// <summary>
    ///// Step value for <see cref="double"/> ranges.
    ///// </summary>
    //public double StepR64 { get; }

    ///// <summary>
    ///// From value for enum ranges.
    ///// </summary>
    //public object? From { get; }

    ///// <summary>
    ///// To value for enum ranges.
    ///// </summary>
    //public object? To { get; }

    /// <summary>
    /// Create a range from two enum values.
    /// </summary>
    /// <param name="from">Lower-bound enum value.</param>
    /// <param name="to">Upper-bound enum value.</param>
    /// <remarks>Enum values do not support stepping.</remarks>
    public RangeAttribute(object from, object to)
    {
        //From = from;
        //To = to;
        //Type = DataType.Enum;
    }

    /// <summary>
    /// Create a range from <see cref="int"/> values.
    /// </summary>
    /// <remarks>If the data type is a smaller integer it will be converted.</remarks>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(int from, int to, int step = 1)
    {
        //FromI32 = from;
        //ToI32 = to;
        //StepI32 = step;
        //Type = DataType.Int32;
    }

    /// <summary>
    /// Create a range from <see cref="char"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Number of characters to increment value by for each test.</param>
    public RangeAttribute(char from, char to, int step = 1)
    {
        //FromI32 = from;
        //ToI32 = to;
        //StepI32 = step;
        //Type = DataType.Int32;
    }

    /// <summary>
    /// Create a range from <see cref="uint"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(uint from, uint to, uint step = 1)
    {
        //FromU32 = from;
        //ToU32 = to;
        //StepU32 = step;
        //Type = DataType.UInt32;
    }

    /// <summary>
    /// Create a range from <see cref="long"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(long from, long to, long step = 1)
    {
        //FromI64 = from;
        //ToI64 = to;
        //StepI64 = step;
        //Type = DataType.Int64;
    }

    /// <summary>
    /// Create a range from <see cref="ulong"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(ulong from, ulong to, ulong step = 1)
    {
        //FromU64 = from;
        //ToU64 = to;
        //StepU64 = step;
        //Type = DataType.UInt64;
    }

    /// <summary>
    /// Create a range from <see cref="float"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(float from, float to, float step = 1f)
    {
        //FromR32 = from;
        //ToR32 = to;
        //StepR32 = step;
        //Type = DataType.Float32;
    }

    /// <summary>
    /// Create a range from <see cref="double"/> values.
    /// </summary>
    /// <param name="from">Lower-bound value.</param>
    /// <param name="to">Upper-bound value.</param>
    /// <param name="step">Amount to increment value by for each test.</param>
    public RangeAttribute(double from, double to, double step = 1f)
    {
        //FromR64 = from;
        //ToR64 = to;
        //StepR64 = step;
        //Type = DataType.Float64;
    }

    ///// <summary>
    ///// Represents the data type stored by a <see cref="RangeAttribute"/>.
    ///// </summary>
    //public enum DataType
    //{
    //    /// <summary><see cref="int"/>, <see cref="char"/>, <see cref="ushort"/>, <see cref="short"/>, <see cref="byte"/>, <see cref="sbyte"/>.</summary>
    //    Int32,
    //
    //    /// <summary><see cref="uint"/></summary>
    //    UInt32,
    //
    //    /// <summary><see cref="long"/></summary>
    //    Int64,
    //
    //    /// <summary><see cref="ulong"/></summary>
    //    UInt64,
    //
    //    /// <summary><see cref="float"/></summary>
    //    Float32,
    //
    //    /// <summary><see cref="double"/></summary>
    //    Float64,
    //
    //    /// <summary>Enums</summary>
    //    /// <remarks>It's possible other types of data like strings may be entered by the user.</remarks>
    //    Enum
    //}
}