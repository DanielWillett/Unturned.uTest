namespace uTest.Sandbox;

[Test]
public class Tests2 : ITestClass, ITestClassSetup
{
    private bool _setupRan;

    [Test]
    public void Test1()
    {
        Assert.That(_setupRan);
    }

    /// <inheritdoc />
    public ValueTask SetupAsync(ITestContext textContext, CancellationToken token)
    {
        Assert.That(_setupRan, Is.False);
        _setupRan = true;
        return default;
    }
}