using System;

namespace uTest;

// copied from https://github.com/UncreatedStaff/UncreatedWarfare/blob/master/UncreatedWarfare/Util/TerminalColorHelper.cs

internal static class TerminalColorHelper
{
    /// <summary>
    /// ANSI escape character for virtual terminal sequences.
    /// </summary>>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public const char ConsoleEscapeCharacter = '\e';

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the foreground color.
    /// </summary>
    public const string ForegroundResetSequence = "\e[39m";

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the background color.
    /// </summary>
    public const string BackgroundResetSequence = "\e[49m";

    public const int DefaultForeground = -9013642;  // gray
    public const int DefaultBackground = -15987700; // black

#pragma warning disable CS8500

    /// <summary>
    /// Wraps text in an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static unsafe string WrapMessageWithTerminalColorSequence(ConsoleColor color, ReadOnlySpan<char> message, bool background = false)
    {
        WrapMessageWithColor8BitState state;
        state.ColorLength = GetTerminalColorSequenceLength(color, background);

        int length = state.ColorLength + message.Length + ForegroundResetSequence.Length;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        state.Message = &message;
        state.Color = color;
        state.Background = background;
        return string.Create(length, state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.Color, state.Background);
            ReadOnlySpan<char> reset = state.Background ? BackgroundResetSequence : ForegroundResetSequence;
            reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
            state.Message->CopyTo(span[state.ColorLength..]);
        });
#else
        Span<char> span = stackalloc char[length];
        WriteTerminalColorSequence(span, color, background);
        ReadOnlySpan<char> reset = background ? BackgroundResetSequence : ForegroundResetSequence;
        reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
        message.CopyTo(span[state.ColorLength..]);
        return span.ToString();
#endif
    }

    private unsafe struct WrapMessageWithColor8BitState
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ReadOnlySpan<char>* Message;
        public ConsoleColor Color;
        public bool Background;
#endif
        public int ColorLength;
    }

    /// <summary>
    /// Wraps text in an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string WrapMessageWithTerminalColorSequence(int argb, ReadOnlySpan<char> message, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return WrapMessageWithTerminalColorSequence(color, message, background);
            }

            return WrapMessageWithTerminalColorSequence((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, message, background);
        }
    }

    /// <summary>
    /// Wraps text in an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static unsafe string WrapMessageWithTerminalColorSequence(byte r, byte g, byte b, ReadOnlySpan<char> message, bool background = false)
    {
        WrapMessageWithColorRGBState state;
        state.ColorLength = GetTerminalColorSequenceLength(r, g, b, background);

        int length = state.ColorLength + message.Length + ForegroundResetSequence.Length;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        state.Message = &message;
        state.R = r;
        state.G = g;
        state.B = b;
        state.Background = background;
        return string.Create(length, state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.R, state.G, state.B, state.Background);
            ReadOnlySpan<char> reset = state.Background ? BackgroundResetSequence : ForegroundResetSequence;
            reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
            state.Message->CopyTo(span[state.ColorLength..]);
        });
#else
        Span<char> span = stackalloc char[length];
        WriteTerminalColorSequence(span, r, g, b, background);
        ReadOnlySpan<char> reset = background ? BackgroundResetSequence : ForegroundResetSequence;
        reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
        message.CopyTo(span[state.ColorLength..]);
        return span.ToString();
#endif
    }

    private unsafe struct WrapMessageWithColorRGBState
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ReadOnlySpan<char>* Message;
        public byte R;
        public byte G;
        public byte B;
        public bool Background;
#endif
        public int ColorLength;
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> value to an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static string GetTerminalColorSequence(ConsoleColor color, bool background = false)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        GetTerminalColorSequence8BitState state = default;
        state.Color = color;
        state.Background = background;
        return string.Create(GetTerminalColorSequenceLength(color, background), state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.Color, state.Background);
        });
#else
        Span<char> span = stackalloc char[GetTerminalColorSequenceLength(color, background)];
        WriteTerminalColorSequence(span, color, background);
        return span.ToString();
#endif
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    private struct GetTerminalColorSequence8BitState
    {
        public ConsoleColor Color;
        public bool Background;
    }
#endif

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string GetTerminalColorSequence(int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return GetTerminalColorSequence(color, background);
            }

            return GetTerminalColorSequence((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, background);
        }
    }

    /// <summary>
    /// Converts an RGB value to an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string GetTerminalColorSequence(byte r, byte g, byte b, bool background = false)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        GetTerminalColorSequenceRGBState state = default;
        state.Background = background;
        state.R = r;
        state.G = g;
        state.B = b;
        return string.Create(GetTerminalColorSequenceLength(r, g, b, background), state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.R, state.G, state.B, state.Background);
        });
#else
        Span<char> span = stackalloc char[GetTerminalColorSequenceLength(r, g, b, background)];
        WriteTerminalColorSequence(span, r, g, b, background);
        return span.ToString();
#endif
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    private struct GetTerminalColorSequenceRGBState
    {
        public byte R;
        public byte G;
        public byte B;
        public bool Background;
    }
#endif

    /// <summary>
    /// Gets the length of a <see cref="ConsoleColor"/> value as an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int GetTerminalColorSequenceLength(ConsoleColor color, bool background = false)
    {
        return background && color is >= ConsoleColor.DarkGray and <= ConsoleColor.White ? 6 : 5;
    }

    /// <summary>
    /// Gets the length of an ARGB value as an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int GetTerminalColorSequenceLength(int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return GetTerminalColorSequenceLength(color, background);
            }

            byte r = (byte)(argb >> 16), g = (byte)(argb >> 8), b = (byte)argb;
            return GetTerminalColorSequenceLength(r, g, b, background);
        }
    }

    /// <summary>
    /// Gets the length of an RGB value as an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int GetTerminalColorSequenceLength(byte r, byte g, byte b, bool background = false)
    {
        return 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
    }

    /// <summary>
    /// Gets the number used to start a foreground or background color in an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int GetTerminalColorSequenceCode(ConsoleColor color, bool background = false)
    {
        ReadOnlySpan<int> colorCodes = [30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97];
        int num = color is < 0 or > ConsoleColor.White ? 39 : colorCodes[(int)color];
        return background ? num + 10 : num;
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> value to an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, ConsoleColor color, bool background = false)
    {
        int num = GetTerminalColorSequenceCode(color, background);

        data[0] = '\u001b';
        data[1] = '[';

        if (num <= 99)
        {
            data[2] = (char)(num / 10 + 48);
            data[3] = (char)(num % 10 + 48);
            data[4] = 'm';
            return 5;
        }

        data[2] = (char)(num / 100 + 48);
        data[3] = (char)(num / 10 % 10 + 48);
        data[4] = (char)(num % 10 + 48);
        data[5] = 'm';
        return 6;
    }

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return WriteTerminalColorSequence(data, color, background);
            }

            byte r = (byte)(argb >> 16), g = (byte)(argb >> 8), b = (byte)argb;
            return WriteTerminalColorSequence(data, r, g, b, background);
        }
    }

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, byte r, byte g, byte b, bool background = false)
    {
        // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors
        data[0] = ConsoleEscapeCharacter;
        data[1] = '[';
        data[2] = background ? '4' : '3';
        data[3] = '8';
        data[4] = ';';
        data[5] = '2';
        data[6] = ';';
        int index = 6;
        if (r > 99)
            data[++index] = (char)(r / 100 + 48);
        if (r > 9)
            data[++index] = (char)((r % 100) / 10 + 48);
        data[++index] = (char)(r % 10 + 48);
        data[++index] = ';';
        if (g > 99)
            data[++index] = (char)(g / 100 + 48);
        if (g > 9)
            data[++index] = (char)((g % 100) / 10 + 48);
        data[++index] = (char)(g % 10 + 48);
        data[++index] = ';';
        if (b > 99)
            data[++index] = (char)(b / 100 + 48);
        if (b > 9)
            data[++index] = (char)((b % 100) / 10 + 48);
        data[++index] = (char)(b % 10 + 48);
        data[++index] = 'm';
        return index + 1;
    }
#pragma warning restore CS8500

    /// <summary>
    /// Convert to <see cref="ConsoleColor"/> to an int which will be reinterpreted as ARGB later on. This is done by making the alpha value zero.
    /// </summary>
    public static int ToArgbRepresentation(ConsoleColor color) => (int)color;

    /// <summary>
    /// Convert a <see cref="Color32"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color32 color)
    {
        if (color.a == 0)
            color.a = byte.MaxValue;

        return color.a << 24 |
               color.r << 16 |
               color.g << 8 |
               color.b;
    }

    /// <summary>
    /// Convert a <see cref="Color"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color color)
    {
        return (byte)Math.Min(255, Mathf.RoundToInt(color.a * 255)) << 24 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.r * 255)) << 16 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.g * 255)) << 8 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.b * 255));
    }

    /// <summary>
    /// Get the closest <see cref="ConsoleColor"/> to the given ARGB data.
    /// </summary>
    public static ConsoleColor ToConsoleColor(int argb)
    {
        int bits = ((argb >> 16) & byte.MaxValue) > 128 || ((argb >> 8) & byte.MaxValue) > 128 || (argb & byte.MaxValue) > 128 ? 8 : 0;
        if (((argb >> 16) & byte.MaxValue) > 180)
            bits |= 4;
        if (((argb >> 8) & byte.MaxValue) > 180)
            bits |= 2;
        if ((argb & byte.MaxValue) > 180)
            bits |= 1;
        return (ConsoleColor)bits;
    }

    /// <summary>
    /// Get a <see cref="Color"/> estimation of <paramref name="color"/>.
    /// </summary>
    public static Color FromConsoleColor(ConsoleColor color)
    {
        int c = (int)color;
        float r = 0f, g = 0f, b = 0f;
        if ((c & 8) == 8)
        {
            r += 0.5f;
            g += 0.5f;
            b += 0.5f;
        }
        if ((c & 4) == 4)
            r += 0.25f;
        if ((c & 2) == 2)
            g += 0.25f;
        if ((c & 1) == 1)
            b += 0.25f;
        return new Color(r, g, b);
    }

    /// <summary>
    /// Effeciently removes any virtual terminal sequences from a string and returns the result as a copy.
    /// </summary>
    public static unsafe string RemoveVirtualTerminalSequences(ReadOnlySpan<char> orig)
    {
        if (orig.Length < 5)
            return orig.ToString();

        bool found = false;
        int l = orig.Length;
        for (int i = 0; i < l; ++i)
        {
            if (orig[i] == ConsoleEscapeCharacter)
            {
                found = true;
            }
        }

        if (!found)
            return orig.ToString();

        // regex: \u001B\[[\d;]*m

        int outpInd = 0;
        char* outp = stackalloc char[l - 3];
        fixed (char* chars = orig)
        {
            int lastCpy = -1;
            for (int i = 0; i < l - 2; ++i)
            {
                if (l <= i + 3 || chars[i] != ConsoleEscapeCharacter || chars[i + 1] != '[' || !char.IsDigit(chars[i + 2]))
                    continue;

                int st = i;
                int c = i + 3;
                for (; c < l; ++c)
                {
                    if (chars[c] != ';' && !char.IsDigit(chars[c]))
                    {
                        if (chars[c] == 'm')
                            i = c;

                        break;
                    }

                    i = c;
                }

                Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (st - lastCpy - 1) * sizeof(char));
                outpInd += st - lastCpy - 1;
                lastCpy += st - lastCpy + (c - st);
            }
            Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (l - lastCpy) * sizeof(char));
            outpInd += l - lastCpy;
        }

        return new string(outp, 0, outpInd - 1);
    }
}