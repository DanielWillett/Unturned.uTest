using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;

namespace uTest.Adapter;

internal class ConsoleMessageLogger : IMessageLogger
{
    private readonly object _consoleSync = new object();

    public static ConsoleMessageLogger Instance { get; } = new ConsoleMessageLogger();

    private ConsoleMessageLogger() { }
    static ConsoleMessageLogger() { }

    /// <inheritdoc />
    public void SendMessage(TestMessageLevel testMessageLevel, string message)
    {
        lock (_consoleSync)
        {
            ConsoleColor fg = Console.ForegroundColor;

            Console.ForegroundColor = testMessageLevel switch
            {
                TestMessageLevel.Warning => ConsoleColor.Yellow,
                TestMessageLevel.Error   => ConsoleColor.Red,
                _                        => ConsoleColor.Gray
            };

            Console.WriteLine(message);

            Console.ForegroundColor = fg;
        }
    }
}
