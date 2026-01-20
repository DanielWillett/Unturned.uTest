using Unturned.SystemEx;

namespace uTest.Dummies;

/// <summary>
/// A dummy player which is simulated on and controlled by the server.
/// Suitable for most tasks but may have some minor differences than <see cref="RemoteDummyPlayerActor"/> players.
/// </summary>
public sealed class SimulatedDummyPlayerActor : BaseServersidePlayerActor
{
    internal ITransportConnection? TransportConnection;
    internal ushort RemotePort;
    internal IPv4Address RemoteAddress;

    public override bool IsRemotePlayer => false;

    /// <inheritdoc />
    internal SimulatedDummyPlayerActor(Player player, SimulatedDummyManager dummyManager, int index)
        : base(index, player.channel.owner.playerID.steamID, player.channel.owner.playerID.playerName, dummyManager)
    {

    }
}