namespace uTest.Sandbox;

[Test]
public class Tests2 : ITestClass, ITestClassSetup
{
    private bool _setupRan;

    [PlayerSimulationMode(PlayerSimulationMode.Full)]
    [PlayerCount(1)]
    [Test]
    public async Task Test1()
    {
        await TestContext.Current.SpawnAllPlayersAsync();

        await GameThread.Switch();

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