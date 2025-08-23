using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// Wrapper struct for ray masks.
/// </summary>
public readonly struct ActorMask : IEquatable<ActorMask>
{
    /// <summary>
    /// All layers are enabled.
    /// </summary>
    public static readonly ActorMask All = new ActorMask(-1);

    /// <summary>
    /// All layers are disabled.
    /// </summary>
    public static readonly ActorMask None = new ActorMask(0);

    /// <summary>
    /// Default ray mask that contains most actors.
    /// </summary>
    public static readonly ActorMask Default = new ActorMask(
        (Dedicator.isStandaloneDedicatedServer ? RayMasks.AGENT : RayMasks.ENTITY)
        | (RayMasks.ENEMY
            | RayMasks.ITEM
            | RayMasks.RESOURCE
            | RayMasks.LARGE
            | RayMasks.MEDIUM
            | RayMasks.SMALL
            | RayMasks.ENVIRONMENT
            | RayMasks.GROUND
            | RayMasks.CLIP
            | RayMasks.VEHICLE
            | RayMasks.BARRICADE
            | RayMasks.STRUCTURE
            | RayMasks.TIRE
            | RayMasks.GROUND2
            ) // they're not redundant
        );

    /// <summary>
    /// Bit mask specifying which layers are included in raycasts.
    /// </summary>
    [ValueProvider("SDG.Unturned.RayMasks")]
    public int RayMask { get; }

    public ActorMask([ValueProvider("SDG.Unturned.RayMasks")] int rayMask)
    {
        RayMask = rayMask;
    }

    public ActorMask(ERayMask rayMask)
    {
        RayMask = (int)rayMask;
    }

    public static ActorMask Build(Action<IActorMaskBuilder> maskBuilder)
    {
        ActorMaskBuilder builder = new ActorMaskBuilder();
        maskBuilder(builder);
        return builder.Mask;
    }

    /// <summary>
    /// Creates an <see cref="ActorMask"/> from types implementing <see cref="IHitInfo"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Invalid type (types are hardcoded).</exception>
    public static ActorMask FromHitInfoType<THitInfo>() where THitInfo : struct, IHitInfo
    {
        if (typeof(THitInfo) == typeof(BarricadeHit))
        {
            return new ActorMask(RayMasks.BARRICADE);
        }
        if (typeof(THitInfo) == typeof(StructureHit))
        {
            return new ActorMask(RayMasks.STRUCTURE);
        }
        if (typeof(THitInfo) == typeof(ResourceHit))
        {
            return new ActorMask(RayMasks.RESOURCE);
        }
        if (typeof(THitInfo) == typeof(PlayerHit))
        {
            return new ActorMask(RayMasks.ENEMY);
        }
        if (typeof(THitInfo) == typeof(ZombieHit) || typeof(THitInfo) == typeof(AnimalHit))
        {
            return new ActorMask(Dedicator.isStandaloneDedicatedServer ? RayMasks.AGENT : RayMasks.ENTITY);
        }
        if (typeof(THitInfo) == typeof(VehicleHit))
        {
            return new ActorMask(RayMasks.VEHICLE);
        }
        if (typeof(THitInfo) == typeof(ObjectHit))
        {
            return new ActorMask(RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL);
        }
        if (typeof(THitInfo) == typeof(RoadHit))
        {
            return new ActorMask(RayMasks.ENVIRONMENT);
        }

        throw new ArgumentOutOfRangeException(nameof(THitInfo), typeof(THitInfo).FullName, Properties.Resources.HitInfoUnknownType);
    }

    public static implicit operator ActorMask([ValueProvider("SDG.Unturned.RayMasks")] int rayMask)
    {
        return new ActorMask(rayMask);
    }

    public static implicit operator ActorMask(ERayMask rayMask)
    {
        return new ActorMask(rayMask);
    }

    private class ActorMaskBuilder : IActorMaskBuilder
    {
        private int _value;

        public ActorMask Mask => new ActorMask(_value);

        /// <inheritdoc />
        public IActorMaskBuilder Add([ValueProvider("SDG.Unturned.RayMasks")] int rayMask)
        {
            _value |= rayMask;
            return this;
        }

        /// <inheritdoc />
        public IActorMaskBuilder Add(ERayMask rayMask)
        {
            _value |= (int)rayMask;
            return this;
        }

        /// <inheritdoc />
        public IActorMaskBuilder Remove([ValueProvider("SDG.Unturned.RayMasks")] int rayMask)
        {
            _value &= ~rayMask;
            return this;
        }

        /// <inheritdoc />
        public IActorMaskBuilder Remove(ERayMask rayMask)
        {
            _value &= ~(int)rayMask;
            return this;
        }
    }

    /// <inheritdoc />
    public bool Equals(ActorMask other)
    {
        return RayMask == other.RayMask;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ActorMask m && Equals(m);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return RayMask;
    }

    public static bool operator ==(ActorMask l, ActorMask r) => l.Equals(r);
    public static bool operator !=(ActorMask l, ActorMask r) => !l.Equals(r);
    public static bool operator <(ActorMask l, ActorMask r) => l.RayMask < r.RayMask;
    public static bool operator >(ActorMask l, ActorMask r) => l.RayMask > r.RayMask;
    public static bool operator <=(ActorMask l, ActorMask r) => l.RayMask <= r.RayMask;
    public static bool operator >=(ActorMask l, ActorMask r) => l.RayMask >= r.RayMask;
    public static ActorMask operator <<(ActorMask l, ActorMask r) => new ActorMask(l.RayMask << r.RayMask);
    public static ActorMask operator <<(ActorMask l, int r) => new ActorMask(l.RayMask << r);
    public static ActorMask operator >>(ActorMask l, ActorMask r) => new ActorMask(l.RayMask >> r.RayMask);
    public static ActorMask operator >>(ActorMask l, int r) => new ActorMask(l.RayMask >> r);
    public static ActorMask operator >>>(ActorMask l, ActorMask r) => new ActorMask(l.RayMask >>> r.RayMask);
    public static ActorMask operator >>>(ActorMask l, int r) => new ActorMask(l.RayMask >>> r);
    public static ActorMask operator &(ActorMask l, ActorMask r) => new ActorMask(l.RayMask & r.RayMask);
    public static ActorMask operator &(ActorMask l, int r) => new ActorMask(l.RayMask & r);
    public static ActorMask operator &(int l, ActorMask r) => new ActorMask(l & r.RayMask);
    public static ActorMask operator |(ActorMask l, ActorMask r) => new ActorMask(l.RayMask | r.RayMask);
    public static ActorMask operator |(ActorMask l, int r) => new ActorMask(l.RayMask | r);
    public static ActorMask operator |(int l, ActorMask r) => new ActorMask(l | r.RayMask);
    public static ActorMask operator ^(ActorMask l, ActorMask r) => new ActorMask(l.RayMask ^ r.RayMask);
    public static ActorMask operator ^(ActorMask l, int r) => new ActorMask(l.RayMask ^ r);
    public static ActorMask operator ^(int l, ActorMask r) => new ActorMask(l ^ r.RayMask);
}

public interface IActorMaskBuilder
{
    ActorMask Mask { get; }
    IActorMaskBuilder Add([ValueProvider("SDG.Unturned.RayMasks")] int rayMask);
    IActorMaskBuilder Add(ERayMask rayMask);
    IActorMaskBuilder Remove([ValueProvider("SDG.Unturned.RayMasks")] int rayMask);
    IActorMaskBuilder Remove(ERayMask rayMask);
}

public static class ActorMaskBuilderExtensions
{

    extension(IActorMaskBuilder builder)
    {
        /// <summary>
        /// Adds all default layers from <see cref="ActorMask.Default"/> to the builder.
        /// </summary>
        public IActorMaskBuilder AddDefault()
        {
            builder.Add(ActorMask.Default.RayMask);
            return builder;
        }

        /// <summary>
        /// Enables all layers.
        /// </summary>
        public IActorMaskBuilder Fill()
        {
            builder.Add(-1);
            return builder;
        }

        /// <summary>
        /// Disables all layers.
        /// </summary>
        public IActorMaskBuilder Clear()
        {
            builder.Remove(-1);
            return builder;
        }
    }
}