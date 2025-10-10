namespace uTest.Sandbox.OtherNameSpace;

[Test]
public class Tests3 : ITestClass, ITestClassSetup, ITestClassTearDown
{
    private bool _setupRan1;
    private bool _setupRan2;
    [Test]
    public void Test1()
    {

    }

    /// <inheritdoc />
    public async ValueTask SetupAsync(ITestContext testContext, CancellationToken token)
    {
        Assert.False(_setupRan1);
        _setupRan1 = true;
        await Task.Delay(10, token);
        Assert.False(_setupRan2);
        _setupRan2 = true;
    }

    /// <inheritdoc />
    public async ValueTask TearDownAsync(CancellationToken token)
    {
        Assert.True(_setupRan1);
        Assert.True(_setupRan2);
        await Task.Delay(10, token);
        Assert.True(_setupRan1);
        Assert.True(_setupRan2);
    }
}