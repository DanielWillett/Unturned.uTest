namespace uTest.Sandbox;

[Test]
public class Tests : ITestClass
{
    [Test]
    public void SomeTest()
    {
        System.Console.WriteLine("Test");
    }

    [Test]
    public void Overloads()
    {
        
    }

    [Test]
    public void Overloads(ref int withOverload)
    {
        
    }

    [Test]
    public void Overloads(ref int withOverload, ref int t2)
    {
        
    }
}