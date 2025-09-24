using System;
using uTest;
using uTest.Logging;
using TestAttribute = NUnit.Framework.TestAttribute;

namespace uTest_Test;

public class InstallDirTests
{
    [Test]
    public void Client()
    {
        InstallDirUtility util = new InstallDirUtility(false, ConsoleLogger.Instance);

        string installDir = util.InstallDirectory;

        Console.WriteLine(installDir);
    }

    [Test]
    public void Server()
    {
        InstallDirUtility util = new InstallDirUtility(true, ConsoleLogger.Instance);

        string installDir = util.InstallDirectory;

        Console.WriteLine(installDir);
    }
}
