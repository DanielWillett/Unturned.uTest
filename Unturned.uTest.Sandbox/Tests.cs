[assembly:
    RequiredWorkshopItem(3456355722 /* Yellowknife */),
    RequiredMap("Yellowknife"),
    RequiredWorkshopItem(2839462324 /* Uncreated Content */),
    RequiredWorkshopItem(2407740920 /* Uncreated Assets */)
]

namespace uTest.Sandbox;


[Test]
[TypeArgs(typeof(long))]
[TypeArgs(typeof(int))]
public class Tests<T> : ITestClass
{
    [Test]
    [TestArgs(null, 1)]
    [TypeArgs(typeof(string))]
    public void TestGeneric<[Set(typeof(long), typeof(int))] T2>(T2[] t2, T num)
    {
        Assert.Null(t2);
    }

    [Test]
    [TestArgs(null, 1)]
    [TypeArgs(typeof(string))]
    public void TestGeneric<[Set(typeof(long), typeof(int))] T2>(T2[] t2, int num)
    {
        Assert.Null(t2);
    }

    //[Test]
    //public void ParameterizedTestFromRange(
    //    [Range(ConsoleColor.Black, ConsoleColor.Yellow)]
    //    ConsoleColor testInt,
    //
    //    [Range(0, 15, step: 5)] [Set(21, 18)]
    //    int spacing
    //)
    //{
    //    Test.Fail();
    //}

    //[Test]
    //[TestArgs(-1)]
    //public void ParameterizedTestFromSet(
    //    [Range(0, 16383)]
    //    int str
    //)
    //{
    //    Test.Fail();
    //}

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
        Assert.Equal('a', testInt);
    }
    
    private static readonly Array ParseNumberArgs2 = new[]
    {
        new { arr = new string[] { "3", "5", "7" } },
        new { arr = new string[] { "3", "4", "7" } },
        new { arr = new string[] { "3", "3" } }
    };

    [Test]
    [TestArgs(From = nameof(ParseNumberArgs2))]
    public void ParameterizedTestFromMemberUnparsable(string[] arr)
    {
        Assert.NotEmpty(arr);
    }

    [Test]
    [TestArgs('a', (byte)13)]
    [TestArgs(3, 16)]
    [TestArgs(3, "18")]
    public void ParameterizedTestFromArgs(char testInt, int iterations)
    {
        Assert.NotEqual('\0', testInt);
    }

    [Test]
    //[RequiredAssets(RequiredAssetsAttribute.Source.Core, "Items/**")]
    public void FastTest()
    {
        CommandWindow.Log("Test log from unturned");
        Console.WriteLine("Test log from console.");
        Thread.Sleep(10);
        Assert.Fail();
    }

    [Test]
    public void SlowTest()
    {
        Thread.Sleep(5000);
    }
}