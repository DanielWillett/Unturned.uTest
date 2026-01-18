namespace uTest.Sandbox.PlayerConnectionTests;

[Test]
[PlayerCount(3), PlayerSimulationMode(PlayerSimulationMode.Full)]
public class MultiplePlayers : ITestClass
{
    [Test]
    public async Task SpawnAllPlayers()
    {
        // spawn all players
        await TestContext.Current.SpawnAllPlayersAsync();
        Assert.Equal(3, Provider.clients.Count);
        
        await Task.Delay(500);


        // despawn a player
        await TestContext.Current.Players[0].DespawnAsync();
        Assert.Equal(2, Provider.clients.Count);

        await Task.Delay(500);



        // despawn remaining players
        await TestContext.Current.DespawnAllPlayersAsync();
        Assert.Equal(0, Provider.clients.Count);



        await Task.Delay(500);

        // try respawning a player
        await TestContext.Current.Players[0].SpawnAsync();
        Assert.Equal(1, Provider.clients.Count);
    }
}
