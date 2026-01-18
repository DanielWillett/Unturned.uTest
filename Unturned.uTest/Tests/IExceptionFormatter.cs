using System;

namespace uTest;

/// <summary>
/// Used to format exception output to be displayed in the editor.
/// </summary>
public interface IExceptionFormatter
{
    /// <summary>
    /// Gets the string representation of an exception.
    /// </summary>
    string FormatException(Exception ex);
}
