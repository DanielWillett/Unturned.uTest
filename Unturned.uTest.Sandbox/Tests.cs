namespace uTest.Sandbox;

[Test]
public class Tests : ITestClass
{
    [Test]
    public void ParameterizedTestFromRange(
        [Range(ConsoleColor.Black, ConsoleColor.Yellow)]
        ConsoleColor testInt,

        [Range(0, 15, step: 5)]
        int spacing
    )
    {
        Test.Fail();
    }

    [Test]
    public void ParameterizedTestFromSet(
        [Set("string1", "", "string2")]
        string str
    )
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
    [TestArgs(From = nameof(ParseNumberArgs))]
    public void ParameterizedTestFromMember(char testInt, int iterations)
    {
        Test.Fail();
    }

    [Test]
    [TestArgs('a', (byte)13)]
    [TestArgs(3, 16)]
    [TestArgs(3, "18")]
    public void ParameterizedTestFromArgs(char testInt, int iterations)
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