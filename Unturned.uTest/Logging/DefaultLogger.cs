using System;

namespace uTest.Logging;

/// <summary>
/// Provides the appropriate type of logger given the current environment.
/// </summary>
public static class DefaultLogger
{
    /// <summary>
    /// A logger that logs to the correct output location.
    /// <para>On U3DS, this is <see cref="CommandWindow"/>.</para>
    /// <para>On Unturned, this is <see cref="UnturnedLog"/>.</para>
    /// <para>When running outside of unturned, this logs to <see cref="Console"/>.</para>
    /// </summary>
    public static ILogger Logger { get; }

    static DefaultLogger()
    {
        // Unturned not started up
        if (ThreadUtil.gameThread == null)
        {
            Logger = ConsoleLogger.Instance;
            return;
        }

        // client doesn't need to log to the command window
        if (Dedicator.isStandaloneDedicatedServer)
            Logger = CommandWindowLogger.Instance;
        else
            Logger = UnturnedLogLogger.Instance;
    }
}