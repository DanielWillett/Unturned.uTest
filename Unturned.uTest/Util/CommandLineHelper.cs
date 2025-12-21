using System;
// ReSharper disable LocalizableElement

namespace uTest;

internal static class CommandLineHelper
{
    private static readonly TextEscaper Escaper = new TextEscaper('"');

    internal static string EscapeCommandLineArg(ReadOnlySpan<char> input) => EscapeCommandLineArg(input, null);

    [return: NotNullIfNotNull(nameof(inputStr))]
    internal static string? EscapeCommandLineArg(string inputStr) => inputStr == null ? null : EscapeCommandLineArg(inputStr.AsSpan(), inputStr);

    internal static string EscapeCommandLineArg(ReadOnlySpan<char> input, string? inputStr)
    {
        int index = input.IndexOf('\\');
        if (index >= 0)
            throw new ArgumentException("Command line args can not contain backslashes. This is an Unturned limitation.", nameof(input));
        
        string escaped = inputStr != null ? Escaper.Escape(inputStr) : Escaper.Escape(input);
        if (input.Length != escaped.Length)
            return $"\"{escaped}\"";
        
        for (int i = 0; i < escaped.Length; ++i)
        {
            if (char.IsWhiteSpace(escaped[i]))
                return $"\"{escaped}\"";
        }

        return escaped;
    }
}
