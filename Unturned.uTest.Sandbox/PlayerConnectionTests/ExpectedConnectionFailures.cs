namespace uTest.Sandbox.PlayerConnectionTests;

#pragma warning disable CS8618

[Test]
[PlayerCount(1), PlayerSimulationMode(PlayerSimulationMode.Full)]
public class ExpectedConnectionFailures : ITestClassSetup
{
    private IServersideTestPlayer _player;

    public ValueTask SetupAsync(ITestContext testContext, CancellationToken token)
    {
        _player = testContext.Players[0];
        return default;
    }

    [Test]
    public async Task TestCanUsuallyConnect()
    {
        await _player.SpawnAsync();

        Assert.Connected(_player);

        await Task.Delay(1000);
    }
    
    [Test]
    public async Task TestLevelHash()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectLevelHash = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.HASH_LEVEL);
    }
    
    [Test]
    public async Task TestAssemblyHash()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectAssemblyHash = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.HASH_ASSEMBLY);
    }
    
    [Test]
    public async Task TestResourceHash()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectResourceHash = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.HASH_RESOURCES);
    }

    [Test]
    public async Task TestEconHash()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectEconHash = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.ECON_HASH);
    }

    [Test]
    public async Task TestGameVersion()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectGameVersion = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.VERSION);
    }

    [Test]
    public async Task TestMapVersion()
    {
        await Assert.ThrowsAsync<ActorDestroyedException>(async () =>
        {
            await _player.SpawnAsync(x => x.UseCorrectMapVersion = false);
        });

        Assert.Rejected(_player, ESteamConnectionFailureInfo.LEVEL_VERSION);
    }
}
