using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// An actor for a <see cref="StructureDrop"/>.
/// </summary>
public class StructureActor : IPlaceableTestActor
{
    /// <summary>
    /// The underlying <see cref="StructureDrop"/> this actor represents.
    /// </summary>
    public StructureDrop Drop { get; }

    /// <summary>
    /// The serverside data for the structure this actor represents.
    /// </summary>
    /// <remarks>Only null when <see cref="Provider.isServer"/> is <see langword="false"/>.</remarks>
    public StructureData? Data { get; }

    /// <summary>
    /// Create a new structure actor given it's drop and serverside data, if on a server.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mismatched instance IDs.</exception>
    public StructureActor(StructureDrop drop, StructureData? data)
    {
        if (drop == null)
            throw new ArgumentNullException(nameof(drop));
        if (data == null)
        {
            if (Provider.isServer)
                throw new ArgumentNullException(nameof(data));
        }
        else if (Provider.isServer)
        {
            if (drop.instanceID != data.instanceID)
                throw new ArgumentException(Properties.Resources.PlaceablesInconsistantInstanceIDs, nameof(data));
            Data = data;
        }

        Drop = drop;
    }

    /// <inheritdoc />
    public bool IsAlive => Drop.GetNetId().id != 0;

    /// <inheritdoc />
    public string DisplayName => Drop.asset.itemName ?? Drop.asset.name;

    /// <inheritdoc />
    public NetId? NetId
    {
        get
        {
            NetId nId = Drop.GetNetId();
            return nId.IsNull() ? null : nId;
        }
    }

    /// <inheritdoc />
    public Vector3 Position
    {
        get
        {
            if (Data != null)
            {
                Vector3 pt = Data.point;
                AssertAlive();
                return pt;
            }

            if (GameThread.IsCurrent)
            {
                AssertAlive();
                return Drop.model == null ? throw new ActorMissingAuthorityException(this) : Drop.model.position;
            }

            return GameThread.RunAndWait(this, static me =>
            {
                me.AssertAlive();
                return me.Drop.model == null ? throw new ActorMissingAuthorityException(me) : me.Drop.model.position;
            });
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, pos: value), static args => args.me.Position = args.pos);
                return;
            }

            AssertAlive();
            AssertInBounds();
            if (Data == null)
                throw new ActorMissingAuthorityException(this);

            EventToggle.Invoke((me: this, pos: value), static args =>
            {
                if (args.me.Data == null)
                {
                    if (args.me.Drop.model == null)
                        throw new ActorDestroyedException(args.me);

                    args.me.AssertCanRequestStructureTransform();
                    StructureManager.transformStructure(args.me.Drop.model, args.pos, args.me.Drop.model.rotation);
                }
                else
                {
                    StructureManager.ServerSetStructureTransform(args.me.Drop.model, args.pos, args.me.Data.rotation);
                }
            });
        }
    }

    /// <inheritdoc />
    public Quaternion Rotation
    {
        get
        {
            if (Data != null)
            {
                Quaternion rot = Data.rotation;
                AssertAlive();
                return rot;
            }

            if (GameThread.IsCurrent)
            {
                AssertAlive();
                return Drop.model == null ? throw new ActorMissingAuthorityException(this) : Drop.model.rotation;
            }

            return GameThread.RunAndWait(this, static me =>
            {
                me.AssertAlive();
                return me.Drop.model == null ? throw new ActorMissingAuthorityException(me) : me.Drop.model.rotation;
            });
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, rot: value), static args => args.me.Rotation = args.rot);
                return;
            }

            AssertAlive();
            AssertInBounds();

            EventToggle.Invoke((me: this, rot: value), static args =>
            {
                if (args.me.Data == null)
                {
                    if (args.me.Drop.model == null)
                        throw new ActorDestroyedException(args.me);

                    args.me.AssertCanRequestStructureTransform();
                    StructureManager.transformStructure(args.me.Drop.model, args.me.Drop.model.position, args.rot);
                }
                else
                {
                    StructureManager.ServerSetStructureTransform(args.me.Drop.model, args.me.Data.point, args.rot);
                }
            });
        }
    }

    /// <inheritdoc />
    public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        if (!GameThread.IsCurrent)
        {
            GameThread.RunAndWait((me: this, position, rotation), static args => args.me.SetPositionAndRotation(args.position, args.rotation));
            return;
        }

        AssertAlive();
        AssertInBounds();

        EventToggle.Invoke((me: this, pos: position, rot: rotation), static args =>
        {
            if (args.me.Data == null)
            {
                args.me.AssertCanRequestStructureTransform();
                StructureManager.transformStructure(args.me.Drop.model, args.pos, args.rot);
            }
            else
            {
                StructureManager.ServerSetStructureTransform(args.me.Drop.model, args.pos, args.rot);
            }
        });
    }

    /// <inheritdoc />
    public double Health
    {
        get
        {
            if (Data == null)
                throw new ActorMissingAuthorityException(this);

            return Data.structure.health;
        }
        set
        {
            if (value is > ushort.MaxValue or < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait(value, hp => Health = hp);
                return;
            }

            AssertAlive();
            AssertInBounds();

            if (Data == null)
                throw new ActorMissingAuthorityException(this);

            ushort health = (ushort)Math.Round(value);
            ushort currentHealth = Data.structure.health;
            if (health == currentHealth)
                return;

            if (health > currentHealth)
            {
                float hp = health - currentHealth;
                EventToggle.Invoke((me: this, hp), static amt => StructureManager.repair(amt.me.Drop.model, amt.hp, 1f));
            }
            else
            {
                float hp = currentHealth - health;
                EventToggle.Invoke((me: this, hp), static amt => StructureManager.damage(amt.me.Drop.model, Vector3.zero, amt.hp, 1f, false));
            }
        }
    }

    /// <inheritdoc />
    public double MaximumHealth => Drop.asset.health;

    /// <inheritdoc />
    public void Kill()
    {
        if (!GameThread.IsCurrent)
        {
            GameThread.RunAndWait(this, static me => me.Kill());
            return;
        }

        AssertAlive();

        if (Data == null)
            throw new ActorMissingAuthorityException(this);

        EventToggle.Invoke(this, static me =>
        {
            me.AssertInBounds(out byte x, out byte y);
            StructureManager.destroyStructure(me.Drop, x, y, Vector3.zero);
        });
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is StructureActor a && a.Drop.instanceID == Drop.instanceID;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is StructureActor a && a.Drop.instanceID == Drop.instanceID;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return unchecked( (int)Drop.instanceID );
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Structure: {DisplayName} (#{Drop.instanceID})";
    }

    public static bool operator ==(StructureActor? a, StructureActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(StructureActor? a, StructureActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator StructureDrop?(StructureActor? actor) => actor?.Drop;

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator StructureData?(StructureActor? actor) => actor?.Data;

    Vector3 ITestActor.Scale
    {
        get
        {
            AssertAlive();
            return Vector3.one;
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetScaleStructure);
    }

    private void AssertCanRequestStructureTransform()
    {
        if (Data != null)
            throw new InvalidOperationException();
        if (Player.LocalPlayer == null || Player.LocalPlayer.life.isDead || !Player.LocalPlayer.look.canUseWorkzone)
            throw new ActorMissingAuthorityException(this);
    }

    private void AssertAlive()
    {
        if (!IsAlive)
            throw new ActorDestroyedException(this);
    }

    private void AssertInBounds()
    {
        if (Regions.checkSafe(Drop.model.position))
        {
            return;
        }

        throw new ActorOutOfBoundsException(this);
    }

    private void AssertInBounds(out byte x, out byte y)
    {
        if (!StructureManager.tryGetRegion(Drop.model, out x, out y, out _))
            throw new ActorOutOfBoundsException(this);
    }

    /// <summary>
    /// This structure's asset.
    /// </summary>
    public ItemStructureAsset Asset => Drop.asset;

    /// <inheritdoc />
    ItemPlaceableAsset IPlaceableTestActor.Asset => Drop.asset;

    /// <inheritdoc />
    PlaceableType IPlaceableTestActor.Type => PlaceableType.Structure;

    /// <inheritdoc />
    public uint InstanceId => Drop.instanceID;

    /// <inheritdoc />
    public T GetDrop<T>() where T : class => (T)(object)Drop;

    /// <inheritdoc />
    public T GetServersideData<T>() where T : class
    {
        if (Data == null)
            throw new ActorMissingAuthorityException(this);

        return (T)(object)Data;
    }
}