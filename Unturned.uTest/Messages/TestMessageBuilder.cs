using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace uTest.Messages;

[SkipLocalsInit]
[InterpolatedStringHandler]
[DebuggerStepThrough]
public struct TestMessageBuilder
{
    private readonly IFormatProvider? _formatProvider;

    private const int PerFormatEstimateLength = 8;

    private char[] _buffer;
    private int _length;

    public TestMessageBuilder(int literalLength, int formattedCount) : this(literalLength, formattedCount, null) { }

    public TestMessageBuilder(int literalLength, int formattedCount, IFormatProvider? formatProvider)
    {
        _formatProvider = formatProvider;
        _buffer = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * PerFormatEstimateLength);
        _length = 0;
    }

    private void TryExtend(int len)
    {
        if (_length + len > _buffer.Length)
        {
            Extend(len);
        }
    }

    private void TryExtend(int len, int alignment)
    {
        len = Math.Max(len, Math.Abs(alignment));
        if (_length + len > _buffer.Length)
        {
            Extend(len);
        }
    }

    private void Align(int len, int alignment)
    {
        int spaces = Math.Abs(alignment) - len;
        if (spaces <= 0)
            return;

        _buffer.AsSpan(_length, spaces).Fill(' ');
        _length += spaces;
    }

    private void Extend(int len)
    {
        char[] newArr = ArrayPool<char>.Shared.Rent(_length + len + 32);
        Buffer.BlockCopy(_buffer, 0, newArr, 0, _length * sizeof(char));
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newArr;
    }

    internal string GetString()
    {
        if (_buffer == null)
            return string.Empty;

        string str = _length == 0 ? string.Empty : new string(_buffer, 0, _length);
        ArrayPool<char>.Shared.Return(_buffer, false);
        _buffer = null!;
        return str;
    }

    /// <summary>
    /// Returns the current string value this handler is building.
    /// </summary>
    public readonly override string ToString()
    {
        return _length == 0 ? string.Empty : new string(_buffer, 0, _length);
    }

    /// <summary>
    /// Appends a string to this handler.
    /// </summary>
    public void AppendLiteral(string? s)
    {
        if (s == null)
            return;

        int strLen = s.Length;
        TryExtend(strLen);
        s.CopyTo(0, _buffer, _length, strLen);
        _length += strLen;
    }

    /// <summary>
    /// Appends a string to this handler.
    /// </summary>
    public void AppendFormatted(string? value)
    {
        AppendLiteral(value);
    }

    /// <summary>
    /// Appends a string to this handler.
    /// </summary>
    public void AppendFormatted(string? value, string? format)
    {
        AppendLiteral(value);
    }

    /// <summary>
    /// Appends a string to this handler.
    /// </summary>
    public void AppendFormatted(string? value, int alignment, string format)
    {
        AppendFormatted(value, alignment);
    }

    /// <summary>
    /// Appends a string to this handler.
    /// </summary>
    public void AppendFormatted(string? value, int alignment)
    {
        if (value == null)
        {
            TryExtend(Math.Abs(alignment));
            Align(0, alignment);
            return;
        }

        int strLen = value.Length;
        TryExtend(strLen, alignment);

        if (alignment < 0)
            Align(strLen, alignment);

        value.CopyTo(0, _buffer, _length, strLen);
        _length += strLen;

        if (alignment > 0)
            Align(strLen, alignment);
    }

    /// <summary>
    /// Appends a character span to this handler.
    /// </summary>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        int strLen = value.Length;
        TryExtend(strLen);
        value.CopyTo(_buffer.AsSpan(_length));
        _length += strLen;
    }

    /// <summary>
    /// Appends a character span to this handler.
    /// </summary>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment, string format)
    {
        AppendFormatted(value, alignment);
    }

    /// <summary>
    /// Appends a character span to this handler.
    /// </summary>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        int strLen = value.Length;
        TryExtend(strLen, alignment);

        if (alignment < 0)
            Align(strLen, alignment);

        value.CopyTo(_buffer.AsSpan(_length));
        _length += strLen;

        if (alignment > 0)
            Align(strLen, alignment);
    }

    /// <summary>
    /// Appends a value to this handler.
    /// </summary>
    public void AppendFormatted<T>(T value)
    {
        AppendFormatted(value, 0, null);
    }

    /// <summary>
    /// Appends a value to this handler.
    /// </summary>
    public void AppendFormatted<T>(T value, int alignment)
    {
        AppendFormatted(value, alignment, null);
    }

    /// <summary>
    /// Appends a value to this handler.
    /// </summary>
    public void AppendFormatted<T>(T value, string? format)
    {
        AppendFormatted(value, 0, format);
    }

    /// <summary>
    /// Appends a value to this handler.
    /// </summary>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (value == null)
        {
            return;
        }

        if (typeof(T).IsEnum)
        {
            string s = value.ToString();
            AppendFormatted(s, alignment);
            return;
        }

#if NETSTANDARD2_1_OR_GREATER
        AppendFormattedDelegate<T>? tryFormat = SpanFormat<T>.TryFormat;
        if (tryFormat != null)
        {
            int extended = 0;
            int charsWritten;
            TryExtend(PerFormatEstimateLength);
            while (!tryFormat(value, _buffer.AsSpan(_length), out charsWritten, ReadOnlySpan<char>.Empty, _formatProvider))
            {
                extended += PerFormatEstimateLength;
                TryExtend(extended);
            }

            _length += charsWritten;

            int alignCt = Math.Abs(alignment);
            if (alignCt <= charsWritten)
                return;

            int spaces = alignCt - charsWritten;
            TryExtend(spaces);
            if (alignment < 0)
            {
                Align(charsWritten, alignment);
            }
            else if (alignment > 0)
            {
                int startPos = _length - charsWritten;
                for (int i = 0; i < charsWritten; ++i)
                    _buffer[startPos + spaces + i] = _buffer[startPos + i];
                _buffer.AsSpan(startPos, alignment).Fill(' ');
            }

            return;
        }
#endif

        string formatted;
        if (value is IFormattable formattable)
        {
            formatted = formattable.ToString(format, _formatProvider);
        }
        else
        {
            formatted = value.ToString();
        }

        if (alignment == 0)
            AppendLiteral(formatted);
        else
            AppendFormatted(formatted, alignment);
    }

#if NETSTANDARD2_1_OR_GREATER
    private static class SpanFormat<T>
    {
        public static AppendFormattedDelegate<T>? TryFormat;
        static SpanFormat()
        {
            if (typeof(T) == typeof(byte))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<byte>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(short))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<short>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(ushort))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<ushort>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(decimal))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<decimal>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(double))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<double>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(long))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<long>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(ulong))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<ulong>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(IntPtr))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<IntPtr>((value, destination, out charsWritten, format, formatProvider)
                        => ((long)value).TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(UIntPtr))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<UIntPtr>((value, destination, out charsWritten, format, formatProvider)
                        => ((ulong)value).TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(BigInteger))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<BigInteger>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(uint))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<uint>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(float))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<float>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(sbyte))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<sbyte>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(DateTime))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<DateTime>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(DateTimeOffset))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<DateTimeOffset>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(int))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<int>((value, destination, out charsWritten, format, formatProvider)
                        => value.TryFormat(destination, out charsWritten, format, formatProvider));
            }
            else if (typeof(T) == typeof(Guid))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<Guid>((value, destination, out charsWritten, format, _)
                        => value.TryFormat(destination, out charsWritten, format));
            }
            else if (typeof(T) == typeof(TimeSpan))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<TimeSpan>((value, destination, out charsWritten, format, _)
                        => value.TryFormat(destination, out charsWritten, format));
            }
            else if (typeof(T) == typeof(IPAddress))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<IPAddress>((value, destination, out charsWritten, _, _)
                        => value.TryFormat(destination, out charsWritten));
            }
            else if (typeof(T) == typeof(Version))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<Version>((value, destination, out charsWritten, _, _)
                        => value.TryFormat(destination, out charsWritten));
            }
            else if (typeof(T) == typeof(string))
            {
                TryFormat = (AppendFormattedDelegate<T>)(object)
                    new AppendFormattedDelegate<string>((value, destination, out charsWritten, _, _)
                        =>
                    {
                        if (destination.Length > value.Length)
                        {
                            charsWritten = 0;
                            return false;
                        }

                        charsWritten = value.Length;
                        value.AsSpan().CopyTo(destination);
                        return true;
                    });
            }
        }
    }

    private delegate bool AppendFormattedDelegate<in T>(T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider);
#endif
}
