namespace uTest.OtherNameSpace;

[Test]
public class Tests3 : ITestClass, ITestClassTearDown
{
    [Test]
    public void Test1()
    {

    }

    /// <inheritdoc />
    public ValueTask TearDownAsync(CancellationToken token)
    {
        return default;
    }
}