using System.Collections;

namespace uTest.Sandbox;

[Test]
[TypeArgs(typeof(long))]
public class Tests<[Set(typeof(string), typeof(Version))] T> : ITestClass, IEnumerable<T>
{
    [Test]
    [TestArgs(1, 2)]
    [TypeArgs(typeof(string))]
    public unsafe void TestGeneric<[Set(typeof(long), typeof(int))] T2>(List<T> t1, T2[] t2, T* ptr)
    {
        Test.Pass();
    }

    [Test]
    public void ParameterizedTestFromRange(
        //[Range(ConsoleColor.Black, ConsoleColor.Yellow)]
        ConsoleColor testInt,

        //[Range(0, 15, step: 5)] [Set(21, 18)]
        int spacing
    )
    {
        Test.Fail();
    }

    [Test]
    [TestArgs(-1)]
    public void ParameterizedTestFromSet(
        //[Range(0, 16383)]
        int str
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

    /// <inheritdoc />
    [Test]
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null!;

    /// <inheritdoc />
    [Test]
    IEnumerator IEnumerable.GetEnumerator() => throw null!;
}