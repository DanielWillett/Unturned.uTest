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

        await TestContext.Current.SpawnAllPlayersAsync();
        await GameThread.Switch();

        Assert.True(Level.isLoaded);
        Assert.False(Level.isExiting);
        Assert.False(Level.isEditor);
        Assert.Equal("PEI", Level.info.name);
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