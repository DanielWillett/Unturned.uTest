namespace uTest.Sandbox;

[Test]
public class Tests2 : ITestClass, ITestClassSetup
{
    private bool _setupRan;

    [Test]
    public void Test1()
    {
        Assert.True(_setupRan);
    }

    /// <inheritdoc />
    public ValueTask SetupAsync(ITestContext textContext, CancellationToken token)
    {
        Assert.False(_setupRan);
        _setupRan = true;
        return default;
    }
}