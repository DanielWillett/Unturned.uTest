using System;

namespace uTest;

internal sealed class DateTimeOffsetFullComparer : IEqualityComparer<DateTimeOffset>
{
    public static DateTimeOffsetFullComparer Instance = new DateTimeOffsetFullComparer();
    private DateTimeOffsetFullComparer() { }
    static DateTimeOffsetFullComparer() { }

    /// <inheritdoc />
    public bool Equals(DateTimeOffset x, DateTimeOffset y)
    {
        return x.Offset.Ticks == y.Offset.Ticks && x.UtcTicks == y.UtcTicks;
    }

    /// <inheritdoc />
    public int GetHashCode(DateTimeOffset obj)
    {
        return obj.GetHashCode() + (int)Math.Round(obj.Offset.TotalMinutes);
    }
}
