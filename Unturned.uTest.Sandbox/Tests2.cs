using uTest.Environment;

namespace uTest.Sandbox;

[Test]
public class Tests2 : ITestClass, ITestClassSetup
{
    private bool _setupRan;

    [Test]
    public void Test1(ITestEnvironment env)
    {
        IServersideTestPlayer player = env.Players.OfType<IServersideTestPlayer>().First();

        Assert.True(_setupRan);
    }

    /// <inheritdoc />
    public ValueTask SetupAsync(ITestContext testContext, CancellationToken token)
    {
        testContext.ConfigureAsync(env =>
        {

        });
        return default;
    }
}