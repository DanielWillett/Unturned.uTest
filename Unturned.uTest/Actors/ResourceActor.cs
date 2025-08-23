using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// A resource or 'tree'. Note that resources always exist but can become alive or dead at any point.
/// </summary>
public class ResourceActor(ResourceSpawnpoint spawnpoint) : ITestActor
{
    /// <summary>
    /// The underlying <see cref="ResourceSpawnpoint"/> this actor represents.
    /// </summary>
    public ResourceSpawnpoint Spawnpoint { get; } = spawnpoint;

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            if (Spawnpoint.asset == null)
                return "#NAME";

            return Spawnpoint.asset.resourceName ?? Spawnpoint.asset.name;
        }
    }

    /// <inheritdoc />
    public NetId? NetId
    {
        get
        {
            NetId nId = NetIdRegistry.GetTransformNetId(Spawnpoint.model);
            return nId.IsNull() ? null : nId;
        }
    }

    /// <inheritdoc />
    public Vector3 Position
    {
        get => Spawnpoint.point;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionTransformResource);
    }

    /// <inheritdoc />
    public Quaternion Rotation
    {
        get => Spawnpoint.angle;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionTransformResource);
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get => Spawnpoint.scale;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionTransformResource);
    }

    /// <inheritdoc />
    void ITestActor.SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionTransformResource);
    }

    /// <inheritdoc />
    public bool IsAlive => !Spawnpoint.isDead;

    /// <inheritdoc />
    public double Health
    {
        get
        {
            AuthorityHelper.AssertServer(this);

            if (Spawnpoint.asset == null)
                return Spawnpoint.health > 0 ? 1 : 0;

            return Spawnpoint.health;
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

            AuthorityHelper.AssertServer(this);

            ushort health;
            if (Spawnpoint.asset == null)
                health = value < 0.5 ? (ushort)0 : (ushort)1;
            else
            {
                health = (ushort)Math.Round(value);
            }

            EventToggle.Invoke((me: this, hp: health), static args =>
            {
                args.me.AssertInBounds(out byte x, out byte y, out ushort index);

                ushort currentHealth = args.me.Spawnpoint.health;
                if (currentHealth == args.hp)
                    return;

                if (args.hp == 0)
                {
                    ResourceManager.ServerSetResourceDead(x, y, index, Vector3.zero);
                    return;
                }

                if (currentHealth == 0)
                {
                    ResourceManager.ServerSetResourceAlive(x, y, index);
                    currentHealth = args.me.Spawnpoint.asset?.health ?? 1;
                    if (args.hp == currentHealth)
                        return;
                }
                else if (args.hp > currentHealth)
                {
                    // no way to 'repair' resources, killing and respawning sets health to full
                    ResourceManager.ServerSetResourceDead(x, y, index, Vector3.zero);
                    ResourceManager.ServerSetResourceAlive(x, y, index);
                    currentHealth = args.me.Spawnpoint.asset?.health ?? 1;
                    if (currentHealth == args.hp)
                        return;
                }

                ResourceManager.damage(args.me.Spawnpoint.model, Vector3.zero, currentHealth - args.hp, times: 1f, drop: 0f, out _, out _);
            });
        }
    }

    /// <inheritdoc />
    public double MaximumHealth => Spawnpoint.asset?.health ?? 1d;

    /// <inheritdoc />
    public void Kill()
    {
        AssertAlive();
        Health = 0;
    }

    private void AssertAlive()
    {
        if (!IsAlive)
            throw new ActorDestroyedException(this);
    }

    private void AssertInBounds(out byte x, out byte y)
    {
        Vector3 pos = Spawnpoint.point;
        double xFull = Math.Floor((pos.x + 4096f) / Regions.REGION_SIZE);
        double yFull = Math.Floor((pos.z + 4096f) / Regions.REGION_SIZE);
        if (xFull is > byte.MaxValue or < 0 || yFull is > byte.MaxValue or < 0)
            throw new ActorOutOfBoundsException(this);

        x = (byte)xFull;
        y = (byte)yFull;
    }

    private void AssertInBounds(out byte x, out byte y, out ushort index)
    {
        AssertInBounds(out x, out y);
        List<ResourceSpawnpoint>? spawnpoints = LevelGround.GetTreesOrNullInRegion(x, y);
        if (spawnpoints == null)
            throw new ActorOutOfBoundsException(this);

        int ind = spawnpoints.IndexOf(Spawnpoint);
        if (ind is -1 or > ushort.MaxValue)
            throw new ActorOutOfBoundsException(this);

        index = (ushort)ind;
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is ResourceActor actor && Spawnpoint == actor.Spawnpoint;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ResourceActor actor && Spawnpoint == actor.Spawnpoint;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Spawnpoint.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Resource: {DisplayName}";
    }

    public static bool operator ==(ResourceActor? a, ResourceActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(ResourceActor? a, ResourceActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator ResourceSpawnpoint?(ResourceActor? actor) => actor?.Spawnpoint;
}