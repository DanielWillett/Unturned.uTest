using System;
using uTest.Compat.Logging;
using uTest.Logging;
using ILogger = uTest.Logging.ILogger;

namespace uTest.Compat.OpenMod.Logging;

public class OpenModLoggerIntegration : ILoggerIntegration
{
    public OpenModLoggerIntegration() { }

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    public bool ShouldHookConsole => true;

    /// <inheritdoc />
    public bool ShouldHookDedicatedIO => false;

    /// <inheritdoc />
    public ILogger CreateNamedLogger(string name) => new SerilogLogger(name);

    /// <inheritdoc />
    public void BeginHook(Action<LogLevel, string> callback) { }

    /// <inheritdoc />
    public void EndHook() { }
}
