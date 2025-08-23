using System;

namespace uTest;

/// <summary>
/// A test actor representing a player, vehicle, barricade, structure, zombie, animal, etc that can be interacted with by the player.
/// </summary>
public interface ITestActor : IEquatable<ITestActor?>
{
    /// <summary>
    /// The display name of this actor.
    /// </summary>
    /// <remarks>For players this is their character name.</remarks>
    string DisplayName { get; }

    /// <summary>
    /// The <see cref="SDG.Unturned.NetId"/> of this actor if it has one.
    /// </summary>
    NetId? NetId { get; }

    /// <summary>
    /// The world position of the actor by it's origin.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to set.</exception>
    /// <exception cref="NotSupportedException">Setting position is not supported for this actor.</exception>
    Vector3 Position { get; set; }

    /// <summary>
    /// The rotation of the actor.
    /// </summary>
    /// <remarks>This may not be as expected for some actors like players, where their pitch is stored in a child object.</remarks>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to set.</exception>
    /// <exception cref="NotSupportedException">Setting rotation is not supported for this actor.</exception>
    Quaternion Rotation { get; set; }

    /// <summary>
    /// The scale of the actor, usually <see cref="Vector3.one"/>.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to set.</exception>
    /// <exception cref="NotSupportedException">Setting scale is not supported for this actor.</exception>
    Vector3 Scale { get; set; }

    /// <summary>
    /// Sets the world position and rotation of the actor by it's origin.
    /// </summary>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to set.</exception>
    /// <exception cref="NotSupportedException">Setting position or rotation is not supported for this actor.</exception>
    void SetPositionAndRotation(Vector3 position, Quaternion rotation);

    /// <summary>
    /// If this actor is still alive.
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// Health value. When setting values rounding may occur.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ArgumentOutOfRangeException">Health is too high or too low for this actor's health data type.</exception>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to get or set.</exception>
    double Health { get; set; }

    /// <summary>
    /// The maximum health of the actor.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to get.</exception>
    double MaximumHealth { get; }

    /// <summary>
    /// Kill this actor.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="ActorDestroyedException"/>
    /// <exception cref="ActorOutOfBoundsException"/>
    /// <exception cref="ActorMissingAuthorityException">No authority to perform an operation.</exception>
    void Kill();
}