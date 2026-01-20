using System;
using uTest.Module;

namespace uTest.Dummies;

/// <summary>
/// Base class for remote and simulated dummies (<see cref="IServersideTestPlayer"/>).
/// </summary>
public abstract class BaseServersidePlayerActor : PlayerActor, IServersideTestPlayer, IDisposable
{
    internal ESteamConnectionFailureInfo? RejectReason;
    internal string? RejectReasonString;
    internal TimeSpan? RejectDuration;
    internal byte[]? HWIDs;
    internal DummyPlayerJoinConfiguration? Configuration;

    private protected IDummyPlayerController PlayerController { get; }

    /// <inheritdoc />
    public abstract bool IsRemotePlayer { get; }

    /// <inheritdoc />
    public bool IsOnline { get; private set; }

    /// <summary>
    /// The test this actor is allocated to.
    /// </summary>
    internal UnturnedTestInstanceData? Test { get; set; }

    /// <inheritdoc cref="IServersideTestPlayer.Index"/>
    public int Index { get; internal set; }

    /// <inheritdoc />
    private protected BaseServersidePlayerActor(int index, CSteamID steam64, string displayName, IDummyPlayerController playerController)
        : base(null, steam64, displayName)
    {
        Index = index;
        PlayerController = playerController;
    }

    [MemberNotNull(nameof(Configuration))]
    internal void Configure(Action<DummyPlayerJoinConfiguration>? configurer)
    {
        GameThread.Assert();
        DummyPlayerJoinConfiguration c = new DummyPlayerJoinConfiguration(Index, Steam64, DisplayName, this is RemoteDummyPlayerActor);
        if (configurer != null)
        {
            configurer(c);
        }

        HWIDs = c.GetHwidPacked();
        Configuration = c;
        Configure(c);
    }

    protected virtual void Configure(DummyPlayerJoinConfiguration c) { }

    /// <inheritdoc />
    public bool TryGetRejectionInfo(out ESteamConnectionFailureInfo info, out string? reason, out TimeSpan? duration)
    {
        if (!RejectReason.HasValue)
        {
            info = ESteamConnectionFailureInfo.NONE;
            reason = null;
            duration = null;
            return false;
        }

        info = RejectReason.Value;
        reason = RejectReasonString;
        duration = RejectDuration;
        if (!duration.HasValue && info == ESteamConnectionFailureInfo.BANNED)
            duration = TimeSpan.Zero;

        return true;
    }

    /// <inheritdoc />
    public Task SpawnAsync(Action<DummyPlayerJoinConfiguration>? configurePlayers = null, bool ignoreAlreadyConnected = false, CancellationToken token = default)
    {
        return PlayerController.SpawnPlayerAsync(this, configurePlayers, ignoreAlreadyConnected, token);
    }

    /// <inheritdoc />
    public Task DespawnAsync(bool ignoreAlreadyDisconnected = false, CancellationToken token = default)
    {
        return PlayerController.DespawnPlayerAsync(this, ignoreAlreadyDisconnected, token);
    }

    internal void NotifyConnected(Player connectedPlayer)
    {
        IsOnline = true;
        NotifyConnectedIntl(connectedPlayer);
    }

    protected virtual void Dispose(bool disposing)
    {

    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }

    int IServersideTestPlayer.Index { get => Index; set => Index = value; }
    UnturnedTestInstanceData? IServersideTestPlayer.Test { get => Test; set => Test = value; }
}