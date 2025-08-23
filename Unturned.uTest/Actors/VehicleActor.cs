using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// An actor for an <see cref="InteractableVehicle"/>.
/// </summary>
public class VehicleActor(InteractableVehicle vehicle) : ITestActor
{
    /// <summary>
    /// The underlying <see cref="InteractableVehicle"/> this actor represents.
    /// </summary>
    public InteractableVehicle Vehicle { get; } = vehicle;

    /// <inheritdoc />
    public NetId? NetId
    {
        get
        {
            NetId nId = Vehicle.GetNetId();
            return nId.IsNull() ? null : nId;
        }
    }

    /// <inheritdoc />
    public string DisplayName => Vehicle.asset.vehicleName ?? Vehicle.asset.name;

    /// <inheritdoc />
    public Vector3 Position
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait(this, static me => me.Position);
            }

            try
            {
                return Vehicle.transform.position;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, pos: value), static args =>
                {
                    args.me.Position = args.pos;
                });
                return;
            }

            AuthorityHelper.AssertServer(this);
            AssertNotDriven();
            try
            {
                Vehicle.transform.position = value;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
    }

    /// <inheritdoc />
    public Quaternion Rotation
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait(this, static me => me.Rotation);
            }

            try
            {
                return Vehicle.transform.rotation;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, rot: value), static args =>
                {
                    args.me.Rotation = args.rot;
                });
                return;
            }

            AuthorityHelper.AssertServer(this);
            AssertNotDriven();
            try
            {
                Vehicle.transform.rotation = value;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get
        {
            AssertNotDead();
            return Vector3.one;
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetScaleVehicle);
    }

    /// <inheritdoc />
    public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        if (!GameThread.IsCurrent)
        {
            GameThread.RunAndWait((me: this, position, rotation), static args =>
            {
                args.me.SetPositionAndRotation(args.position, args.rotation);
            });
            return;
        }

        AuthorityHelper.AssertServer(this);
        AssertNotDriven();
        try
        {
            Vehicle.transform.SetPositionAndRotation(position, rotation);
        }
        catch (NullReferenceException)
        {
            throw new ActorDestroyedException(this);
        }
    }

    /// <inheritdoc />
    /// <returns><see langword="false"/> if the vehicle has been exploded, destroyed, drowned, or fully despawned.</returns>
    public bool IsAlive => Vehicle is { isExploded: false, isDead: false, isDrowned: false } && Vehicle != null;

    /// <inheritdoc />
    public double Health
    {
        get => Vehicle.health;
        set
        {
            if (value is > ushort.MaxValue or < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, hp: value), static args => args.me.Health = args.hp);
                return;
            }

            AuthorityHelper.AssertServer(this);
            if (!IsAlive)
                throw new ActorDestroyedException(this);

            ushort newHealth = (ushort)Math.Round(value);
            ushort currentHealth = Vehicle.health;

            if (newHealth == currentHealth)
                return;

            VehicleManager.sendVehicleHealth(Vehicle, newHealth);
        }
    }

    /// <inheritdoc />
    public double MaximumHealth => Vehicle.asset.health;

    /// <inheritdoc />
    public void Kill()
    {
        Health = 0;
    }

    private void AssertNotDriven()
    {
        if (Vehicle.passengers is { Length: > 0 } && Vehicle.passengers[0].player != null)
        {
            throw new ActorMissingAuthorityException(this, string.Format(Properties.Resources.ActorMissingAuthorityExceptionVehicleDriven, DisplayName));
        }
    }

    private void AssertNotDead()
    {
        if (Vehicle == null)
            throw new ActorDestroyedException(this);
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is VehicleActor actor && actor.Vehicle == Vehicle;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is VehicleActor actor && Vehicle == actor.Vehicle;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Vehicle.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Vehicle: {DisplayName}";
    }

    public static bool operator ==(VehicleActor? a, VehicleActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(VehicleActor? a, VehicleActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator InteractableVehicle?(VehicleActor? actor) => actor?.Vehicle;
}
