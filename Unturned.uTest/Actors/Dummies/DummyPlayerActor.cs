namespace uTest.Dummies;

public class DummyPlayerActor : PlayerActor, IServersideTestPlayer
{
    internal DummyClientTransport ClientTransportIntl;
    public IClientTransport ClientTransport => ClientTransportIntl;
    bool IServersideTestPlayer.IsRealPlayer => false;

    /// <inheritdoc />
    private protected DummyPlayerActor(Player player, DummyClientTransport clientTransport) : base(player)
    {
        ClientTransportIntl = clientTransport;
    }

}
