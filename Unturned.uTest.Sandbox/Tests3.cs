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
    public async ValueTask SetupAsync(ITestContext textContext, CancellationToken token)
    {
        Assert.That(_setupRan1, Is.False);
        _setupRan1 = true;
        await Task.Delay(10, token);
        Assert.That(_setupRan2, Is.False);
        _setupRan2 = true;
    }

    /// <inheritdoc />
    public async ValueTask TearDownAsync(CancellationToken token)
    {
        Assert.That(_setupRan1, Is.True);
        Assert.That(_setupRan2, Is.True);
        await Task.Delay(10, token);
        Assert.That(_setupRan1, Is.True);
        Assert.That(_setupRan2, Is.True);
    }
}