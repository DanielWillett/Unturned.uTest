using Serilog.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ILogger = uTest.Logging.ILogger;
using LogLevel = uTest.Logging.LogLevel;

namespace uTest.Compat.OpenMod.Logging;

/// <summary>
/// A <see cref="ILogger"/> implementation that writes to a Serilog logger.
/// </summary>
public class SerilogLogger : ILogger, IDisposable
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly SerilogLoggerProvider _provider;

    /// <summary>
    /// Creates a <see cref="ILogger"/> implementation for the default static Serilog logger.
    /// </summary>
    /// <inheritdoc cref="SerilogLogger(Serilog.ILogger,string,bool)"/>
    public SerilogLogger(string name, bool dispose = false) : this(Serilog.Log.Logger, name, dispose) { }

    /// <summary>
    /// Creates a <see cref="ILogger"/> implementation for a Serilog logger.
    /// </summary>
    /// <param name="logger">The parent logger.</param>
    /// <param name="name">The name to use as the source context.</param>
    /// <param name="dispose">Whether or not the provided logger will be disposed when this logger is disposed.</param>
    public SerilogLogger(Serilog.ILogger logger, string name, bool dispose = false)
    {
        _provider = new SerilogLoggerProvider(logger, dispose);
        _logger = _provider.CreateLogger(name);
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
        _logger.Log((Microsoft.Extensions.Logging.LogLevel)logLevel, default, state, exception, formatter);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled((Microsoft.Extensions.Logging.LogLevel)logLevel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _provider.Dispose();
    }
}