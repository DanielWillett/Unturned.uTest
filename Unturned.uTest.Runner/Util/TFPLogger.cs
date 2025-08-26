using Microsoft.Testing.Platform.Logging;

namespace uTest.Runner.Util;

internal class TFPLogger : ILogger
{
    private readonly Microsoft.Testing.Platform.Logging.ILogger _logger;

    public TFPLogger(Microsoft.Testing.Platform.Logging.ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Info(string message)
    {
        _logger.LogInformation(message);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        _logger.LogWarning(message);
    }

    /// <inheritdoc />
    public void Error(string message)
    {
        _logger.LogError(message);
    }

    /// <inheritdoc />
    public void Exception(string? message, Exception ex)
    {
        if (message != null && ex != null)
            _logger.LogError(message, ex);
        else if (ex != null)
            _logger.LogError(ex);
        else if (message != null)
            _logger.LogError(message);
    }
}
