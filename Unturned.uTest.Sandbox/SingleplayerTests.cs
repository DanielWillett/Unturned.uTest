using uTest.Logging;

namespace uTest.Sandbox;

[Test]
[PlayerSimulationMode(PlayerSimulationMode.Singleplayer)]
public class SingleplayerTests : ITestClass
{
    [Test]
    [PlayerCount(0)]
    public void MenuTest()
    {
        GameThread.Assert();

        Assert.False(Level.isLoaded);
        Assert.False(Level.isExiting);
        Assert.False(Level.isEditor);
        Assert.False(Player.isLoading);
    }

    [Test]
    [RequiredMap("PEI")]
    public async Task PEITest()
    {
        GameThread.Assert();

        TestContext.Logger.LogInformation("Spawning all players...");

        await TestContext.Current.SpawnAllPlayersAsync();
        TestContext.Logger.LogInformation("Done spawning all players.");
        await GameThread.Switch();

        Assert.True(Level.isLoaded);
        Assert.False(Level.isExiting);
        Assert.False(Level.isEditor);
        Assert.Equal("PEI", Level.info.name);

        await Task.Delay(500);
        TestContext.Logger.LogInformation("Test over.");
    }

    [Test]
    [RequiredMap("Washington")]
    [PlayerCount(1, SpawnAllPlayers = true)]
    public void WashingtonTest()
    {
        GameThread.Assert();

        Assert.True(Level.isLoaded);
        Assert.False(Level.isExiting);
        Assert.False(Level.isEditor);
        Assert.Equal("Washington", Level.info.name);
    }
}