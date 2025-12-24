using System;

namespace uTest.Dummies;

public abstract class BaseServersidePlayerActor : PlayerActor, IServersideTestPlayer, IDisposable
{
    private protected IDummyPlayerController PlayerController { get; }

    /// <inheritdoc />
    public abstract bool IsRemotePlayer { get; }

    /// <inheritdoc />
    public bool IsOnline { get; private set; }

    /// <inheritdoc />
    private protected BaseServersidePlayerActor(CSteamID steam64, string displayName, IDummyPlayerController playerController)
        : base(null, steam64, displayName)
    {
        PlayerController = playerController;
    }

    internal void NotifyConnected(Player connectedPlayer)
    {
        NotifyConnectedIntl(connectedPlayer);
        IsOnline = true;
    }

    protected virtual void Dispose(bool disposing)
    {

    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }
}