[Test]
public class Tests5 : ITestClass
{
    [Test]
    public async Task Test1()
    {
        await Task.Delay(500);
        Assert.Fail();
    }
}