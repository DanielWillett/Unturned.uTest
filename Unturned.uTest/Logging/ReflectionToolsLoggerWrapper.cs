using DanielWillett.ReflectionTools;
using System;

namespace uTest.Logging;

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