using System;
using System.Text;
using DanielWillett.ModularRpcs.Serialization.Parsers;
using DanielWillett.ReflectionTools;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace

namespace uTest.Logging;

#pragma warning restore IDE0130

public interface ILogger
{
    Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    bool IsEnabled(LogLevel logLevel);
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}

public static class DefaultLogger
{
    public static ILogger Logger { get; }

    static DefaultLogger()
    {
        // client doesn't need to log to the command window
        if (Dedicator.isStandaloneDedicatedServer)
            Logger = CommandWindowLogger.Instance;
        else
            Logger = UnturnedLogLogger.Instance;
    }
}

public static class DefaultLoggerReflectionTools
{
    public static IReflectionToolsLogger Logger { get; } = new ReflectionToolsLoggerWrapper(DefaultLogger.Logger);
}

internal sealed class ReflectionToolsLoggerWrapper(ILogger logger) : IReflectionToolsLogger
{
    public void LogDebug(string source, string message)
    {
        logger.LogDebug(string.IsNullOrEmpty(source) ? message : $"[{source}] {message}");
    }

    public void LogInfo(string source, string message)
    {
        logger.LogInformation(string.IsNullOrEmpty(source) ? message : $"[{source}] {message}");
    }

    public void LogWarning(string source, string message)
    {
        logger.LogWarning(string.IsNullOrEmpty(source) ? message : $"[{source}] {message}");
    }

    public void LogError(string source, Exception? ex, string? message)
    {
        if (ex != null)
        {
            if (message == null)
                logger.LogError(ex);
            else
                logger.LogError(string.IsNullOrEmpty(source) ? message : $"[{source}] {message}", ex);
        }
        else
        {
            if (message != null)
                logger.LogError(string.IsNullOrEmpty(source) ? message : $"[{source}] {message}");
        }
    }
}

public sealed class ConsoleLogger : ILogger
{
    public static readonly ConsoleLogger Instance = new ConsoleLogger();

    private ConsoleLogger() { }
    static ConsoleLogger() { }

    private void Write(string message, ConsoleColor color)
    {
        message ??= string.Empty;
        lock (this)
        {
            ConsoleColor clr = Console.ForegroundColor;
            
            Console.ForegroundColor = color;
            Console.WriteLine(message);

            Console.ForegroundColor = clr;
        }
    }

    /// <inheritdoc />
    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Log(logLevel, state, exception, formatter);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ConsoleColor color = logLevel switch
        {
            LogLevel.Critical or LogLevel.Error => ConsoleColor.Red,
            LogLevel.Debug or LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Warning => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };

        string message = formatter(state, null);
        if (exception == null)
        {
            Write(message, color);
            return;
        }

        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(message).Append(System.Environment.NewLine).Append(exception);
        Write(sb.ToString(), color);

        StringBuilderPool.Return(sb);
    }

    public bool IsEnabled(LogLevel logLevel) => true;
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

public sealed class CommandWindowLogger : ILogger
{
    public static readonly CommandWindowLogger Instance = new CommandWindowLogger();

    private CommandWindowLogger() { }
    static CommandWindowLogger() { }

    private static void Write(string message, LogLevel severity)
    {
        switch (severity)
        {
            default:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    CommandWindow.Log(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run<object>(message, CommandWindow.Log);
                    }
                    catch (InvalidOperationException)
                    {
                        CommandWindow.Log(message);
                    }
                }
                break;

            case LogLevel.Warning:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    CommandWindow.LogWarning(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run<object>(message, CommandWindow.LogWarning);
                    }
                    catch (InvalidOperationException)
                    {
                        CommandWindow.LogWarning(message);
                    }
                }
                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    CommandWindow.LogError(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run<object>(message, CommandWindow.LogError);
                    }
                    catch (InvalidOperationException)
                    {
                        CommandWindow.LogError(message);
                    }
                }
                break;
        }
    }

    private static Task WriteAsync(string message, LogLevel severity)
    {
        if (GameThread.IsCurrent)
        {
            Write(message, severity);
            return Task.CompletedTask;
        }

        return severity switch
        {
            LogLevel.Warning                    => GameThread.RunAndWaitAsync<object>(message, CommandWindow.LogWarning),
            LogLevel.Error or LogLevel.Critical => GameThread.RunAndWaitAsync<object>(message, CommandWindow.LogError),
            _                                   => GameThread.RunAndWaitAsync<object>(message, CommandWindow.Log)
        };
    }

    /// <inheritdoc />
    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, null);
        if (exception == null)
        {
            return WriteAsync(message, logLevel);
        }

        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(message).Append(System.Environment.NewLine).Append(exception);
        Task t = WriteAsync(sb.ToString(), logLevel);

        StringBuilderPool.Return(sb);
        return t;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, null);
        if (exception == null)
        {
            Write(message, logLevel);
            return;
        }

        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(message).Append(System.Environment.NewLine).Append(exception);
        Write(sb.ToString(), logLevel);

        StringBuilderPool.Return(sb);
    }

    public bool IsEnabled(LogLevel logLevel) => true;
}

public sealed class UnturnedLogLogger : ILogger
{
    public static readonly UnturnedLogLogger Instance = new UnturnedLogLogger();

    private UnturnedLogLogger() { }
    static UnturnedLogLogger() { }

    private static void Write(string message, LogLevel severity)
    {
        switch (severity)
        {
            default:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    UnturnedLog.info(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run(message, UnturnedLog.info);
                    }
                    catch (InvalidOperationException)
                    {
                        UnturnedLog.info(message);
                    }
                }
                break;

            case LogLevel.Warning:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    UnturnedLog.warn(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run(message, UnturnedLog.warn);
                    }
                    catch (InvalidOperationException)
                    {
                        UnturnedLog.warn(message);
                    }
                }
                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                if (GameThread.IsCurrent || GameThread.HasStartedShuttingDown)
                {
                    UnturnedLog.error(message);
                }
                else
                {
                    try
                    {
                        GameThread.Run(message, UnturnedLog.error);
                    }
                    catch (InvalidOperationException)
                    {
                        UnturnedLog.error(message);
                    }
                }
                break;
        }
    }

    private static Task WriteAsync(string message, LogLevel severity)
    {
        if (GameThread.IsCurrent)
        {
            Write(message, severity);
            return Task.CompletedTask;
        }

        return severity switch
        {
            LogLevel.Warning                    => GameThread.RunAndWaitAsync(message, UnturnedLog.warn),
            LogLevel.Error or LogLevel.Critical => GameThread.RunAndWaitAsync(message, UnturnedLog.error),
            _                                   => GameThread.RunAndWaitAsync(message, UnturnedLog.info)
        };
    }

    /// <inheritdoc />
    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, null);
        if (exception == null)
        {
            return WriteAsync(message, logLevel);
        }

        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(message).Append(System.Environment.NewLine).Append(exception);
        Task t = WriteAsync(sb.ToString(), logLevel);

        StringBuilderPool.Return(sb);
        return t;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, null);
        if (exception == null)
        {
            Write(message, logLevel);
            return;
        }

        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(message).Append(System.Environment.NewLine).Append(exception);
        Write(sb.ToString(), logLevel);

        StringBuilderPool.Return(sb);
    }

    public bool IsEnabled(LogLevel logLevel) => true;
}