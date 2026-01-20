using System;
using uTest.Logging;

namespace uTest.Compat.Logging;

/// <summary>
/// Allows frameworks to define a different system for integrating with logging.
/// <para>
/// Can implement <see cref="IDisposable"/> if necessary.
/// </para>
/// </summary>
/// <remarks>Only the highest priority integration will be used.</remarks>
public interface ILoggerIntegration
{
    int Priority { get; }

    bool ShouldHookConsole { get; }
    bool ShouldHookDedicatedIO { get; }

    ILogger CreateNamedLogger(string name);

    void BeginHook(Action<LogLevel, string> callback);

    void EndHook();
}