using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace uTest;

internal static class MathHelper
{
    public static double Clamp01(double d)
    {
        if (d <= 0)
            return 0;
        if (d >= 1)
            return 1;
        return d;
    }

    internal static bool TryParseInt(ReadOnlySpan<char> span, out int num)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        return int.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out num);
#else
        if (span.Length == 1)
        {
            num = span[0] - '0';
            if (num is >= 0 and < 10)
                return true;
        }

        return int.TryParse(span.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out num);
#endif
    }

    internal static bool TryParseNumber(ReadOnlySpan<char> span, [MaybeNullWhen(false)] out object num)
    {
        if (span.IsEmpty)
        {
            num = null;
            return false;
        }

        if (TryParseInt(span, out int i4))
        {
            num = i4;
            return true;
        }
#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP2_1_OR_GREATER
        string spanAsString = span.ToString();
#endif

        if (span[0] != '-')
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            if (uint.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out uint u4))
            {
                num = u4;
                return true;
            }
#else
            if (span.Length == 1)
            {
                i4 = span[0] - '0';
                if (i4 is >= 0 and < 10)
                {
                    num = (uint)i4;
                    return true;
                }
            }

            if (uint.TryParse(spanAsString, NumberStyles.None, CultureInfo.InvariantCulture, out uint u4))
            {
                num = u4;
                return true;
            }
#endif

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            if (ulong.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out ulong u8))
            {
                num = u8;
                return true;
            }
#else
            if (span.Length == 1)
            {
                i4 = span[0] - '0';
                if (i4 is >= 0 and < 10)
                {
                    num = (ulong)i4;
                    return true;
                }
            }

            if (ulong.TryParse(spanAsString, NumberStyles.None, CultureInfo.InvariantCulture, out ulong u8))
            {
                num = u8;
                return true;
            }
#endif
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        if (long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
        {
            num = i8;
            return true;
        }
#else
        if (span.Length == 1)
        {
            i4 = span[0] - '0';
            if (i4 is >= 0 and < 10)
            {
                num = (long)i4;
                return true;
            }
        }

        if (long.TryParse(spanAsString, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
        {
            num = i8;
            return true;
        }
#endif

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        if (double.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double r8))
        {
            num = r8;
            return true;
        }
#else
        if (span.Length == 1)
        {
            i4 = span[0] - '0';
            if (i4 is >= 0 and < 10)
            {
                num = (double)i4;
                return true;
            }
        }

        if (double.TryParse(spanAsString, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double r8))
        {
            num = r8;
            return true;
        }
#endif

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        if (decimal.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out decimal r16))
        {
            num = r16;
            return true;
        }
#else
        if (span.Length == 1)
        {
            i4 = span[0] - '0';
            if (i4 is >= 0 and < 10)
            {
                num = (decimal)i4;
                return true;
            }
        }

        if (decimal.TryParse(spanAsString, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out decimal r16))
        {
            num = r16;
            return true;
        }
#endif

        if (span.Equals("true", StringComparison.Ordinal))
        {
            num = 1;
            return true;
        }
        if (span.Equals("false", StringComparison.Ordinal))
        {
            num = 0;
            return true;
        }

        num = null;
        return false;
    }

}
