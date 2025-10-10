namespace uTest;

/// <summary>
/// A player actor.
/// </summary>
public interface ITestPlayer : ITestActor
{
    /// <summary>
    /// The player's yaw (Y) rotation.
    /// </summary>
    float Yaw { get; }

    /// <summary>
    /// The player's pitch (X) rotation.
    /// </summary>
    float Pitch { get; }

    /// <summary>
    /// Information about where the player's looking.
    /// </summary>
    ITestPlayerLook Look { get; }

    /// <summary>
    /// Information about the player's inventory.
    /// </summary>
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
    bool IsRealPlayer { get; }
}

public interface ITestPlayerLook
{
    /// <summary>
    /// The origin of the player's look ray.
    /// </summary>
    Vector3 Origin { get; }

    /// <summary>
    /// A vector pointing towards the direction the player's looking.
    /// </summary>
    Vector3 Forward { get; }

    /// <summary>
    /// The rotation of the player's eyes.
    /// </summary>
    Quaternion Rotation { get; }

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <param name="mask">Raycast mask to allow looking through other objects.</param>
    IRaycastResult Raycast(ActorMask mask, float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false);

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    IRaycastResult Raycast(float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false);

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <typeparam name="THitInfo">Return type will have info about this type of object. Using this type parameter also adds this object's ray mask to <paramref name="mask"/>.</typeparam>
    /// <param name="mask">Raycast mask to allow looking through other objects.</param>
    IRaycastResult<THitInfo> Raycast<THitInfo>(ActorMask mask, float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false)
        where THitInfo : struct, IHitInfo;

    /// <summary>
    /// Perform a raycast to see which actor the player is looking at, if any.
    /// </summary>
    /// <typeparam name="THitInfo">Return type will have info about this type of object. Using this type parameter also adds this object's ray mask to the default ray mask.</typeparam>
    IRaycastResult<THitInfo> Raycast<THitInfo>(float maxDistance = PlayerActor.ReachDistance, bool collideWithTriggers = false)
        where THitInfo : struct, IHitInfo;
}

public interface ITestPlayerInventory
{
    
}