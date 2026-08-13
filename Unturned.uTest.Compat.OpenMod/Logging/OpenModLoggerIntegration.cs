using JetBrains.Annotations;
using System;
using uTest.Compat.Logging;
using uTest.Logging;
using ILogger = uTest.Logging.ILogger;

namespace uTest.Compat.OpenMod.Logging;

/// <summary>
/// Implementation of <see cref="ILoggerIntegration"/> to create openmod loggers.
/// </summary>
[UsedImplicitly]
public class OpenModLoggerIntegration : ILoggerIntegration
{
    private readonly LogLevel _minLogLevel;

    /// <summary>
    /// Creates a new logging provider for uTest to log to the OpenMod logger system.
    /// </summary>
    /// <param name="minLogLevel">The minimum level that should be logged.</param>
    [UsedImplicitly]
    public OpenModLoggerIntegration(LogLevel minLogLevel)
    {
        _minLogLevel = minLogLevel;
    }

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    public bool ShouldHookConsole => true;

    /// <inheritdoc />
    public bool ShouldHookDedicatedIO => false;

    /// <inheritdoc />
    public ILogger CreateNamedLogger(string name) => new SerilogLogger(name, _minLogLevel);

    /// <inheritdoc />
    public void BeginHook(Action<LogLevel, string> callback) { }

    /// <inheritdoc />
    public void EndHook() { }
}
