using uTest.Logging;

namespace uTest.Sandbox;



[Test]
public class Tests2 : ITestClass, ITestClassSetup
{
    private const int Players = 3;

    [PlayerSimulationMode(PlayerSimulationMode.Full)]
    [PlayerCount(Players)]
    [Test]
    public async Task Test1()
    {
        TestContext.Logger.LogInformation("Waiting for players to connect.");
        await TestContext.Current.SpawnAllPlayersAsync(player =>
        {
            player.WithSpawnLocation(PlayerTransform.FromRandomPlayerSpawn());
        });
        TestContext.Logger.LogInformation("All players connected.");

        Assert.Equal(Players, Provider.clients.Count);


        await Task.Delay(2000);


        TestContext.Logger.LogInformation("Waiting for players to disconnect.");
        await TestContext.Current.DespawnAllPlayersAsync();
        TestContext.Logger.LogInformation("All players disconnected.");


        Assert.Empty(Provider.clients);

        TestContext.Logger.LogInformation("Passing in 5 seconds...");
        await Task.Delay(5000);
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