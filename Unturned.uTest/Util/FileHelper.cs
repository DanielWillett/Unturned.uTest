using System;
using System.Runtime.InteropServices;

namespace uTest;

internal static class FileHelper
{
    static FileHelper() { }

    internal static StringComparer FileNameComparer { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
