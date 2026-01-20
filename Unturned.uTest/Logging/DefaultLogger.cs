namespace uTest.Logging;

public static class DefaultLogger
{
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