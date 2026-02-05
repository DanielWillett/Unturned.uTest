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
    /// <summary>
    /// Integrations with higher numbers will be preferred over integrations with lower numbers.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Whether or not <c>STDOUT</c> and <c>STDERR</c> should be redirected and recorded during tests.
    /// </summary>
    bool ShouldHookConsole { get; }

    /// <summary>
    /// Whether or not Unturned's built-in dedicated IO system should be hooked into and recorded during tests.
    /// </summary>
    bool ShouldHookDedicatedIO { get; }

    /// <summary>
    /// Creates a <see cref="ILogger"/> with a category/source name.
    /// </summary>
    ILogger CreateNamedLogger(string name);

    /// <summary>
    /// Invoked when a test starts to start recording log messages.
    /// </summary>
    void BeginHook(Action<LogLevel, string> callback);

    /// <summary>
    /// Invoked when a test completed to stop recording log messages.
    /// </summary>
    void EndHook();
}