using System.Reflection;

namespace uTest.Sandbox;

[Test]
public class Tests : ITestClass
{
    [Test]
    public void ParameterizedTest([Range('a', 'z', 2)] char testInt, [Range(9, 6)] sbyte color)
    {
        Test.Fail();
    }

    private static readonly Array ParseNumberArgs = new[]
    {
        new { testInt = 'a', iterations = 51 },
        new { testInt = 'b', iterations = 52 },
        new { testInt = 'c', iterations = 53 },
    };

    [Test]
    [TestArgs('a', (byte)13)]
    [TestArgs(3, 16)]
    [TestArgs(3, "18")]
    [TestArgs(From = "ParseNumberArgs")]
    public void ParameterizedTest2(char testInt, [Set(long.MaxValue, 2, 3)] int iterations)
    {
        Test.Fail();
    }

    [Test]
    public void FastTest()
    {
        Test.Fail();
    }

    [Test]
    public void SlowTest()
    {
        Thread.Sleep(5000);
    }
}