namespace uTest.Sandbox;

[Test]
public class Tests : ITestClass
{
    [Test]
    public void SomeTest()
    {
        System.Console.WriteLine("Test");
    }

    [Test, InProcess]
    public void Overloads()
    {
        Assert.That(decimal.MaxValue, Is.Decimal.Zero);

        Assert.That(-0.0, Is.Positive.Zero);

        IEnumerable<string> args = new string[10];

        Assert.That("", Is.Not.Null);

        Assert.That(args, Is.All(Is.Not.Null));

        Assert.That(DateTimeOffset.UtcNow, Is.GreaterThan(DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(3))).Within(1).Hours);
    }

    [Test]
    public void Overloads(ref int withOverload)
    {
        Test.Pass();
    }

    [Test]
    public void Overloads(ref int withOverload, ref int t2)
    {
        
    }
}