using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// Class containing various methods to ensure values are as they're expected.
/// </summary>
/// <remarks>Extensions can be made using the C# 14 static extension format.</remarks>
[ExcludeFromCodeCoverage, DebuggerStepThrough]
public static class Assert
{
    /// <exception cref="NotSupportedException"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method is inherited from System.Object and isn't part of Assert.", error: true)]
    public new static bool Equals(object? a, object? b)
    {
        throw new NotSupportedException();
    }

    /// <exception cref="NotSupportedException"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method is inherited from System.Object and isn't part of Assert.", error: true)]
    public new static bool ReferenceEquals(object? a, object? b)
    {
        throw new NotSupportedException();
    }
}