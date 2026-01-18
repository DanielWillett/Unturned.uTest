using System;
using uTest.Dummies;
using uTest.Module;

namespace uTest;

/// <summary>
/// A player actor.
/// </summary>
public interface ITestPlayer : ITestActor
{
    /// <summary>
    /// Player ID. Note that with dummies the steam ID may not necessarily be a valid 'individual' Steam64 ID.
    /// </summary>
    ref readonly CSteamID Steam64 { get; }

    /// <summary>
    /// The player's yaw (Y) rotation.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    float Yaw { get; }

    /// <summary>
    /// The player's pitch (X) rotation.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    float Pitch { get; }

    /// <summary>
    /// Information about where the player's looking.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    ITestPlayerLook Look { get; }

    /// <summary>
    /// Information about the player's inventory.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    ITestPlayerInventory Inventory { get; }
}

/// <summary>
/// A player the test runner has control over (a dummy).
/// </summary>
public interface IServersideTestPlayer : ITestPlayer
{
    /// <summary>
    /// A real player is ran from an instance of the Unturned client running on the test runner, as opposed to a simulated dummy.
    /// </summary>
    bool IsRemotePlayer { get; }

    /// <summary>
    /// Whether or not the player has been connected to the server. Serverside test players may not have been connected yet.
    /// </summary>
    bool IsOnline { get; }

    internal UnturnedTestInstanceData? Test { get; set; }

    /// <summary>
    /// If the player was rejected, outputs the reason information for the rejection.
    /// </summary>
    /// <param name="info">The category of reason the player was rejected.</param>
    /// <param name="reason">Specific information about the rejection, depending on the category.</param>
    /// <param name="duration">If <paramref name="info"/> is <see cref="ESteamConnectionFailureInfo.BANNED"/>, the duration of the ban in seconds.</param>
    /// <returns><see langword="true"/> if the player was rejected, otherwise <see langword="false"/>.</returns>
    bool TryGetRejectionInfo(out ESteamConnectionFailureInfo info, out string? reason, out TimeSpan? duration);

    /// <summary>
    /// Spawns this player into the world.
    /// </summary>
    /// <exception cref="InvalidOperationException">Actor is already spawned in.</exception>
    /// <exception cref="TimeoutException">An actor did not connect in time.</exception>
    /// <exception cref="ActorDestroyedException">An actor disconnected/was rejected while connecting.</exception>
    Task SpawnAsync(Action<DummyPlayerJoinConfiguration>? configurePlayers = null, bool ignoreAlreadyConnected = false, CancellationToken token = default);

    /// <summary>
    /// Disconnects this player from the world.
    /// </summary>
    /// <exception cref="InvalidOperationException">Actor is not spawned in.</exception>
    /// <exception cref="TimeoutException">An actor did not disconnect in time.</exception>
    Task DespawnAsync(bool ignoreAlreadyDisconnected = false, CancellationToken token = default);
}

public interface ITestPlayerLook
{
    /// <summary>
    /// The origin of the player's look ray.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    Vector3 Origin { get; }

    /// <summary>
    /// A vector pointing towards the direction the player's looking.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    Vector3 Forward { get; }

    /// <summary>
    /// The rotation of the player's eyes.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    Quaternion Rotation { get; }

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <param name="mask">Raycast mask to allow looking through other objects.</param>
    /// <exception cref="ActorDestroyedException"/>
    IRaycastResult Raycast(ActorMask mask, float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false);

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    IRaycastResult Raycast(float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false);

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <typeparam name="THitInfo">Return type will have info about this type of object. Using this type parameter also adds this object's ray mask to <paramref name="mask"/>.</typeparam>
    /// <param name="mask">Raycast mask to allow looking through other objects.</param>
    /// <exception cref="ActorDestroyedException"/>
    IRaycastResult<THitInfo> Raycast<THitInfo>(ActorMask mask, float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false)
        where THitInfo : struct, IHitInfo;

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <typeparam name="THitInfo">Return type will have info about this type of object. Using this type parameter also adds this object's ray mask to the default ray mask.</typeparam>
    /// <exception cref="ActorDestroyedException"/>
    IRaycastResult<THitInfo> Raycast<THitInfo>(float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false)
        where THitInfo : struct, IHitInfo;
}

public interface ITestPlayerInventory
{
    
}