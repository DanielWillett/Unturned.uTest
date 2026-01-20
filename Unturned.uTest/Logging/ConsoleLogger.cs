using System;
using System.Text;

namespace uTest.Logging;

/// <summary>
/// Thread-safe <see cref="ILogger"/> implementation that logs to the <see cref="Console"/>.
/// </summary>
public sealed class ConsoleLogger : ILogger
{
    /// <summary>
    /// Singleton instance of <see cref="ConsoleLogger"/>.
    /// </summary>
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
        if (logLevel == LogLevel.None)
            return;

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

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
}