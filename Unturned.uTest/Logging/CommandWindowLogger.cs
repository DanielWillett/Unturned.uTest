using System;
using System.Text;

namespace uTest.Logging;

/// <summary>
/// Thread-safe <see cref="ILogger"/> implementation that logs to the <see cref="CommandWindow"/>.
/// </summary>
public sealed class CommandWindowLogger : ILogger
{
    /// <summary>
    /// Singleton instance of <see cref="CommandWindowLogger"/>.
    /// </summary>
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
            LogLevel.Warning => GameThread.RunAndWaitAsync<object>(message, CommandWindow.LogWarning),
            LogLevel.Error or LogLevel.Critical => GameThread.RunAndWaitAsync<object>(message, CommandWindow.LogError),
            _ => GameThread.RunAndWaitAsync<object>(message, CommandWindow.Log)
        };
    }

    /// <inheritdoc />
    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None)
            return Task.CompletedTask;

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
        if (logLevel == LogLevel.None)
            return;

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

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
}