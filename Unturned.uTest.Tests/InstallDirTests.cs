using System;

namespace uTest.Test;

public class InstallDirTests
{
    [NUnit.Framework.Test]
    public void Client()
    {
        InstallDirUtility util = new InstallDirUtility(false, ConsoleLogger.Instance);

        string installDir = util.InstallDirectory;

        Console.WriteLine(installDir);
    }

    [NUnit.Framework.Test]
    public void Server()
    {
        InstallDirUtility util = new InstallDirUtility(true, ConsoleLogger.Instance);

        string installDir = util.InstallDirectory;

        Console.WriteLine(installDir);
    }
}
