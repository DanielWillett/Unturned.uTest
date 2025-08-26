namespace uTest.Sandbox;

[Test]
public class Tests : ITestClass
{
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