namespace uTest;

public interface ITestPlayer : ITestActor
{
    float Yaw { get; }
    float Pitch { get; }

    ITestPlayerLook Look { get; }
    ITestPlayerInventory Inventory { get; }
}

public interface ITestPlayerLook
{
    Vector3 Origin { get; }
    Vector3 Forward { get; }
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