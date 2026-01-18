using System;

namespace uTest.Dummies;

internal interface IDummyPlayerController
{
    Task SpawnPlayerAsync(IServersideTestPlayer player, Action<DummyPlayerJoinConfiguration>? configurePlayers, bool ignoreAlreadyConnected, CancellationToken token);
    Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token);
}