using OpenMod.API.Users;

namespace uTest.Sandbox.PlayerConnectionTests;

[Test]
[PlayerCount(3), PlayerSimulationMode(PlayerSimulationMode.Simulated)]
public class MultiplePlayers : ITestClass
{
    private readonly IUserManager _userManager;

    public MultiplePlayers(IUserManager userManager)
    {
        _userManager = userManager;
    }

    [Test]
    public async Task SpawnAllPlayers()
    {
        // spawn all players
        await TestContext.Current.SpawnAllPlayersAsync();
        Assert.Equal(3, Provider.clients.Count);

        IReadOnlyCollection<IUser> players = await _userManager.GetUsersAsync("player");
                        // dont include offline players in count
        Assert.Equal(3, players.Count(x => x.Session != null));

        await Task.Delay(500);

        // despawn a player
        await TestContext.Current.Players[0].DespawnAsync();
        Assert.Equal(2, Provider.clients.Count);

        await Task.Delay(500);



        // despawn remaining players
        await TestContext.Current.DespawnAllPlayersAsync();
        Assert.Empty(Provider.clients);



        await Task.Delay(500);

        // try respawning a player
        await TestContext.Current.Players[0].SpawnAsync();
        Assert.Equal(1, Provider.clients.Count);
    }
}
