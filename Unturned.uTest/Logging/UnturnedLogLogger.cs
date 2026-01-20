using System;
using System.Text;

namespace uTest.Logging;

/// <summary>
/// Thread-safe <see cref="ILogger"/> implementation that logs to <see cref="UnturnedLog"/>.
/// </summary>
public sealed class UnturnedLogLogger : ILogger
{
    /// <summary>
    /// Singleton instance of <see cref="UnturnedLogLogger"/>.
    /// </summary>
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
            LogLevel.Warning => GameThread.RunAndWaitAsync(message, UnturnedLog.warn),
            LogLevel.Error or LogLevel.Critical => GameThread.RunAndWaitAsync(message, UnturnedLog.error),
            _ => GameThread.RunAndWaitAsync(message, UnturnedLog.info)
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