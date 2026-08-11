namespace uTest.Dummies;

/// <summary>
/// A single instance of this class is created for tests that should take place in singleplayer.
/// The 'spawned in' state of this kind of player is whether or not a world is loaded in singleplayer.
/// </summary>
public sealed class SingleplayerServersideTestPlayer : BaseServersidePlayerActor
{
    /// <inheritdoc />
    public override bool IsRemotePlayer => false;

    internal SingleplayerServersideTestPlayer(SingleplayerDummyManager playerController)
        : base(-1, Provider.client, Provider.clientName, playerController)
    {

    }
}