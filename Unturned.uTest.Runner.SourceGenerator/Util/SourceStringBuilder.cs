using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace uTest.Util;

public sealed class SourceStringBuilder
{
    private int _indent;
    private bool _isNewLine;
    private bool _hasNewLine;
    private readonly IFormatProvider? _formatProvider;
    private readonly string _newLine;

    public StringBuilder UnderlyingBuilder { get; }

    public int IndentSize { get; init; } = 4;
    public bool UseSpaces { get; init; } = true;

    // Environment.NewLine throws RS1035 (see https://github.com/dotnet/roslyn-analyzers/issues/6467)
    public SourceStringBuilder(IFormatProvider? formatProvider = null, string newLine = "\n")
    {
        UnderlyingBuilder = new StringBuilder();
        _formatProvider = formatProvider;
        _newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
    }
    public SourceStringBuilder(int capacity, IFormatProvider? formatProvider = null, string newLine = "\n")
    {
        UnderlyingBuilder = new StringBuilder(capacity);
        _formatProvider = formatProvider;
        _newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
    }
    public SourceStringBuilder(StringBuilder stringBuilder, IFormatProvider? formatProvider = null, string newLine = "\n")
    {
        UnderlyingBuilder = stringBuilder;
        _formatProvider = formatProvider;
        _newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
    }

    private void StartLine()
    {
        if (!_isNewLine)
            return;

        if (UnderlyingBuilder.Length > 0)
        {
            if (!_hasNewLine)
            {
                UnderlyingBuilder.Append(_newLine);
                _hasNewLine = true;
            }
        }

        UnderlyingBuilder.Append(UseSpaces ? ' ' : '\t', _indent);
        _isNewLine = false;
    }

    public SourceStringBuilder Out()
    {
        int amt = UseSpaces ? IndentSize : 1;
        if (_indent < amt)
        {
            Preprocessor("#error HELP");
            //throw new InvalidOperationException("Already fully un-indented");
            _indent = 0;
            return this;
        }

        _indent -= amt;
        return this;
    }

    public SourceStringBuilder In()
    {
        _indent += UseSpaces ? IndentSize : 1;
        return this;
    }

    /// <summary>
    /// Append an empty line.
    /// </summary>
    public SourceStringBuilder Empty()
    {
        UnderlyingBuilder.Append(_newLine);
        _hasNewLine = false;
        return this;
    }

    /// <summary>
    /// Append a single line.
    /// </summary>
    public SourceStringBuilder String(string singleLine)
    {
        _isNewLine = true;
        StartLine();
        UnderlyingBuilder.Append(singleLine);
        _hasNewLine = false;
        return this;
    }

    /// <summary>
    /// Append a single line.
    /// </summary>
    public SourceStringBuilder Preprocessor(string singleLine)
    {
        _isNewLine = true;
        int indent = _indent;
        _indent = 0;
        StartLine();
        UnderlyingBuilder.Append(singleLine);
        _hasNewLine = false;
        _indent = indent;
        return this;
    }

    /// <summary>
    /// Append an interpolated line.
    /// </summary>
    public SourceStringBuilder Build([InterpolatedStringHandlerArgument("")] SourceStringBuilderInterpolatedStringHandler handler)
    {
        return this;
    }

    public override string ToString() => UnderlyingBuilder.ToString();

    [InterpolatedStringHandler]
    public readonly ref struct SourceStringBuilderInterpolatedStringHandler
    {
        private readonly SourceStringBuilder _builder;

        public SourceStringBuilderInterpolatedStringHandler(int literalLength, int formattedCount, SourceStringBuilder @this)
        {
            _builder = @this;
            _builder.UnderlyingBuilder.EnsureCapacity(_builder.UnderlyingBuilder.Length + literalLength + formattedCount * 11);
            _builder._isNewLine = true;
        }

        public void AppendLiteral(string? literal)
        {
            _builder.StartLine();
            if (literal != null)
                _builder.UnderlyingBuilder.Append(literal);
            _builder._hasNewLine = false;
        }

        public void AppendFormatted(string? value)
        {
            _builder.StartLine();
            if (value != null)
                _builder.UnderlyingBuilder.Append(value);
            _builder._hasNewLine = false;
        }

        public void AppendFormatted(string? value, string? format) => AppendFormatted(value);
        public void AppendFormatted(string? value, int alignment, string? format) => AppendFormatted(value, alignment);
        public void AppendFormatted(string? value, int alignment)
        {
            _builder.StartLine();
            int valueLength = value?.Length ?? 0;
            if (alignment < 0)
            {
                alignment = -alignment;
                if (valueLength != 0)
                    _builder.UnderlyingBuilder.Append(value);
                if (alignment > valueLength)
                    _builder.UnderlyingBuilder.Append(' ', alignment - valueLength);
                _builder._hasNewLine = false;
                return;
            }

            if (alignment > valueLength)
                _builder.UnderlyingBuilder.Append(' ', alignment - valueLength);

            if (valueLength != 0)
                _builder.UnderlyingBuilder.Append(value);
            _builder._hasNewLine = false;
        }

        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            _builder.StartLine();
            AppendReadOnlySpan(value);
        }

        public void AppendFormatted(ReadOnlySpan<char> value, string? format) => AppendFormatted(value);
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment, string? format) => AppendFormatted(value, alignment);
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
        {
            _builder.StartLine();
            if (alignment < 0)
            {
                alignment = -alignment;
                AppendReadOnlySpan(value);
                if (alignment > value.Length)
                    _builder.UnderlyingBuilder.Append(' ', alignment - value.Length);
                return;
            }

            if (alignment > value.Length)
                _builder.UnderlyingBuilder.Append(' ', alignment - value.Length);

            AppendReadOnlySpan(value);
        }

        private unsafe void AppendReadOnlySpan(ReadOnlySpan<char> value)
        {
            fixed (char* ptr = value)
            {
                _builder.UnderlyingBuilder.Append(ptr, value.Length);
            }
            _builder._hasNewLine = false;
        }

        public void AppendFormatted<T>(T? value)
        {
            _builder.StartLine();
            AppendFormattedValue(value);
        }

        public void AppendFormatted<T>(T? value, string? format)
        {
            if (value is IFormattable f)
            {
                AppendFormatted(f.ToString(format, _builder._formatProvider));
            }
            else
            {
                AppendFormatted(value);
            }
        }

        public void AppendFormatted<T>(T? value, int alignment, string? format)
        {
            if (value is IFormattable f)
            {
                AppendFormatted(f.ToString(format, _builder._formatProvider), alignment);
            }
            else
            {
                AppendFormatted(value, alignment);
            }
        }

        public void AppendFormatted<T>(T? value, int alignment)
        {
            _builder.StartLine();
            int start = _builder.UnderlyingBuilder.Length;
            AppendFormattedValue(value);
            int length = _builder.UnderlyingBuilder.Length - start;
            if (alignment < 0)
            {
                alignment = -alignment;
                if (alignment > length)
                    _builder.UnderlyingBuilder.Append(' ', alignment - length);
                return;
            }

            if (alignment > length)
                _builder.UnderlyingBuilder.Insert(start, " ", alignment - length);
        }

        private void AppendFormattedValue<T>(T? value)
        {
            IFormatProvider? formatProvider = _builder._formatProvider;
            if (typeof(T) == typeof(bool))
            {
                _builder.UnderlyingBuilder.Append(Unsafe.As<T?, bool>(ref value));
            }
            else if (typeof(T) == typeof(byte))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, byte>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, byte>(ref value));
                }
            }
            else if (typeof(T) == typeof(char))
            {
                _builder.UnderlyingBuilder.Append(Unsafe.As<T?, char>(ref value));
            }
            else if (typeof(T) == typeof(decimal))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, decimal>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, decimal>(ref value));
                }
            }
            else if (typeof(T) == typeof(double))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, double>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, double>(ref value));
                }
            }
            else if (typeof(T) == typeof(float))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, float>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, float>(ref value));
                }
            }
            else if (typeof(T) == typeof(int))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, int>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, int>(ref value));
                }
            }
            else if (typeof(T) == typeof(long))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, long>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, long>(ref value));
                }
            }
            else if (typeof(T) == typeof(sbyte))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, sbyte>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, sbyte>(ref value));
                }
            }
            else if (typeof(T) == typeof(short))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, short>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, short>(ref value));
                }
            }
            else if (typeof(T) == typeof(uint))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, uint>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, uint>(ref value));
                }
            }
            else if (typeof(T) == typeof(ulong))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, ulong>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, ulong>(ref value));
                }
            }
            else if (typeof(T) == typeof(ushort))
            {
                if (formatProvider != null)
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, ushort>(ref value).ToString(formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(Unsafe.As<T?, ushort>(ref value));
                }
            }
            else
            {
                if (formatProvider != null && value is IFormattable f)
                {
                    _builder.UnderlyingBuilder.Append(f.ToString(null, formatProvider));
                }
                else
                {
                    _builder.UnderlyingBuilder.Append(value == null ? string.Empty : value.ToString());
                }
            }

            _builder._hasNewLine = false;
        }
    }
}