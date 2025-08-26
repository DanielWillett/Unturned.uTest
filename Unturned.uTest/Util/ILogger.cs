using System;

namespace uTest;

public interface ILogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Exception(string? message, Exception ex);
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
    public void Info(string message)
    {
        Write(message, ConsoleColor.Gray);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        Write(message, ConsoleColor.Yellow);
    }

    /// <inheritdoc />
    public void Error(string message)
    {
        Write(message, ConsoleColor.Red);
    }

    /// <inheritdoc />
    public void Exception(string? message, Exception ex)
    {
        if (ex == null && string.IsNullOrEmpty(message))
            return;

        if (ex == null)
        {
            Write(message!, ConsoleColor.Red);
            return;
        }

        if (message == null)
        {
            Write(ex.ToString(), ConsoleColor.Red);
        }
        else
        {
            Write(message + System.Environment.NewLine + ex, ConsoleColor.Red);
        }
    }
}

public sealed class CommandWindowLogger : ILogger
{
    public static readonly CommandWindowLogger Instance = new CommandWindowLogger();

    private CommandWindowLogger() { }
    static CommandWindowLogger() { }

    /// <inheritdoc />
    public void Info(string message)
    {
        message ??= string.Empty;
        if (GameThread.IsCurrent)
        {
            CommandWindow.Log(message);
        }
        else
        {
            GameThread.Run(message, CommandWindow.Log);
        }
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        message ??= string.Empty;
        if (GameThread.IsCurrent)
        {
            CommandWindow.LogWarning(message);
        }
        else
        {
            GameThread.Run(message, CommandWindow.LogWarning);
        }
    }

    /// <inheritdoc />
    public void Error(string message)
    {
        message ??= string.Empty;
        if (GameThread.IsCurrent)
        {
            CommandWindow.LogError(message);
        }
        else
        {
            GameThread.Run(message, CommandWindow.LogError);
        }
    }

    /// <inheritdoc />
    public void Exception(string? message, Exception ex)
    {
        if (ex == null && string.IsNullOrEmpty(message))
            return;

        if (ex == null)
        {
            Error(message!);
            return;
        }

        if (message == null)
        {
            if (GameThread.IsCurrent)
            {
                CommandWindow.LogError(ex);
            }
            else
            {
                GameThread.Run(ex, CommandWindow.LogError);
            }
        }
        else
        {
            if (GameThread.IsCurrent)
            {
                CommandWindow.LogError(message);
                CommandWindow.LogError(ex);
            }
            else
            {
                GameThread.Run((message, ex), static args =>
                {
                    CommandWindow.LogError(args.message);
                    CommandWindow.LogError(args.ex);
                });
            }
        }
    }
}