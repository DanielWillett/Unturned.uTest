namespace uTest.Dummies;

/// <summary>
/// A dummy player which is simulated on and controlled by the server.
/// Suitable for most tasks but may have some minor differences than <see cref="RemoteDummyPlayerActor"/> players.
/// </summary>
public sealed class SimulatedDummyPlayerActor : BaseServersidePlayerActor
{
    internal DummyClientTransport ClientTransportIntl;
    public IClientTransport ClientTransport => ClientTransportIntl;

    public override bool IsRemotePlayer => false;

    /// <inheritdoc />
    internal SimulatedDummyPlayerActor(Player player, DummyClientTransport clientTransport, DummyManager dummyManager)
        : base(player.channel.owner.playerID.steamID, player.channel.owner.playerID.playerName, dummyManager)
    {
        ClientTransportIntl = clientTransport;
        NotifyConnected(player);
    }
}
