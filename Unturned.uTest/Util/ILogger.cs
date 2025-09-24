using System;
using System.Text;

#pragma warning disable IDE0130

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

    /// <inheritdoc />
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
        => logger.LogAsync(LogLevel.Error, ex.ToString(), null, Formatter);

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
        => logger.Log(LogLevel.Error, ex.ToString(), null, Formatter);

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
                if (GameThread.IsCurrent)
                {
                    CommandWindow.Log(message);
                }
                else
                {
                    GameThread.Run<object>(message, CommandWindow.Log);
                }
                break;

            case LogLevel.Warning:
                if (GameThread.IsCurrent)
                {
                    CommandWindow.LogWarning(message);
                }
                else
                {
                    GameThread.Run<object>(message, CommandWindow.LogWarning);
                }
                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                if (GameThread.IsCurrent)
                {
                    CommandWindow.LogError(message);
                }
                else
                {
                    GameThread.Run<object>(message, CommandWindow.LogError);
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

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;
}