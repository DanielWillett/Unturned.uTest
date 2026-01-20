using System;
using System.Threading.Tasks;

#pragma warning disable IDE0130

namespace uTest.Logging;

#pragma warning restore IDE0130

/// <summary>
/// Logger for uTest.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs information to the console/log file/etc.
    /// </summary>
    Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    /// <summary>
    /// Logs information to the console/log file/etc.
    /// </summary>
    void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    
    /// <summary>
    /// Checks whether or not a log level should be outputted.
    /// </summary>
    bool IsEnabled(LogLevel logLevel);
}

/// <summary>
/// Log severity level.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Verbose debug information.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Helpful debug information.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Helpful information.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Information that may indicate a problem.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Information that indicates a problematic issue.
    /// </summary>
    Error = 4,
    
    /// <summary>
    /// Information that indicates a fatal issue.
    /// </summary>
    Critical = 5,

    /// <summary>
    /// Information that shouldn't be logged.
    /// </summary>
    None = 6
}

public static class LoggingExtensions
{
    // mostly stolen from MTP
    private static readonly Func<string, Exception?, string> Formatter = (state, _) => state;

    public static Task LogTraceAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Trace, message, null, Formatter);

    public static Task LogDebugAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Debug, message, null, Formatter);

    public static Task LogInformationAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Information, message, null, Formatter);

    public static Task LogWarningAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Warning, message, null, Formatter);

    public static Task LogErrorAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Error, message, null, Formatter);

    public static Task LogErrorAsync(this ILogger logger, string message, Exception ex)
        => logger.LogAsync(LogLevel.Error, message, ex, Formatter);

    public static Task LogErrorAsync(this ILogger logger, Exception ex)
        => logger.LogAsync(LogLevel.Error, string.Empty, ex, Formatter);

    public static Task LogCriticalAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Critical, message, null, Formatter);

    public static void LogTrace(this ILogger logger, string message)
        => logger.Log(LogLevel.Trace, message, null, Formatter);

    public static void LogDebug(this ILogger logger, string message)
        => logger.Log(LogLevel.Debug, message, null, Formatter);

    public static void LogInformation(this ILogger logger, string message)
        => logger.Log(LogLevel.Information, message, null, Formatter);

    public static void LogWarning(this ILogger logger, string message)
        => logger.Log(LogLevel.Warning, message, null, Formatter);

    public static void LogError(this ILogger logger, string message)
        => logger.Log(LogLevel.Error, message, null, Formatter);

    public static void LogError(this ILogger logger, string message, Exception ex)
        => logger.Log(LogLevel.Error, message, ex, Formatter);

    public static void LogError(this ILogger logger, Exception ex)
        => logger.Log(LogLevel.Error, string.Empty, ex, Formatter);

    public static void LogCritical(this ILogger logger, string message)
        => logger.Log(LogLevel.Critical, message, null, Formatter);
}