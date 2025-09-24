using uTest.Logging;

namespace uTest.Runner.Util;

internal class MTPLogger : ILogger
{
    private readonly Microsoft.Testing.Platform.Logging.ILogger _logger;

    public MTPLogger(Microsoft.Testing.Platform.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        return _logger.LogAsync((Microsoft.Testing.Platform.Logging.LogLevel)logLevel, state, exception, formatter);
    }

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log((Microsoft.Testing.Platform.Logging.LogLevel)logLevel, state, exception, formatter);
    }

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled((Microsoft.Testing.Platform.Logging.LogLevel)logLevel);
}
