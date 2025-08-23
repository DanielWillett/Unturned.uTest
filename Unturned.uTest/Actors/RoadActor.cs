using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace uTest;

/// <summary>
/// An actor for a <see cref="SDG.Unturned.Road"/>.
/// </summary>
public class RoadActor(Road road, RoadAsset? roadAsset, byte legacyMaterialIndex) : ITestActor
{
    /// <summary>
    /// The underlying <see cref="SDG.Unturned.Road"/> this actor represents.
    /// </summary>
    public Road Road { get; } = road;

    /// <summary>
    /// Road asset for this road.
    /// </summary>
    public RoadAsset? RoadAsset { get; } = roadAsset;
    
    /// <summary>
    /// Legacy <see cref="RoadMaterial"/> index for old roads. Ignored if <see cref="RoadAsset"/> is not <see langword="null"/>.
    /// </summary>
    public byte LegacyMaterialIndex { get; } = legacyMaterialIndex;

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            if (RoadAsset != null)
            {
                return RoadAsset.FriendlyName;
            }

            if (LevelRoads.materials == null || LegacyMaterialIndex >= LevelRoads.materials.Length)
                return "Road";

            RoadMaterial mat = LevelRoads.materials[LegacyMaterialIndex];
            if (Dedicator.isStandaloneDedicatedServer || mat.material == null)
            {
                return $"#{LegacyMaterialIndex.ToString(CultureInfo.InvariantCulture)}";
            }

            return mat.material.mainTexture.name;
        }
    }

    private void AssertNotDead()
    {
        if (Road.road == null)
            throw new ActorDestroyedException(this);
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is RoadActor actor && actor.Road == Road;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is RoadActor actor && Road == actor.Road;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Road.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Road: {DisplayName}";
    }

    public static bool operator ==(RoadActor? a, RoadActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(RoadActor? a, RoadActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator Road?(RoadActor? actor) => actor?.Road;

    /// <inheritdoc />
    double ITestActor.Health
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionRoadHealth);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionRoadHealth);
    }

    /// <inheritdoc />
    double ITestActor.MaximumHealth => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionRoadHealth);

    /// <inheritdoc />
    void ITestActor.Kill()
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionRoadHealth);
    }

    /// <inheritdoc />
    Vector3 ITestActor.Position
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait<ITestActor, Vector3>(this, static me => me.Position);
            }

            AssertNotDead();
            return Road.road.position;
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformRoad);
    }

    /// <inheritdoc />
    Quaternion ITestActor.Rotation
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait<ITestActor, Quaternion>(this, static me => me.Rotation);
            }

            AssertNotDead();
            return Road.road.rotation;
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformRoad);
    }

    /// <inheritdoc />
    Vector3 ITestActor.Scale
    {
        get => Vector3.one;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformRoad);
    }

    NetId? ITestActor.NetId => null;

    bool ITestActor.IsAlive => true;

    void ITestActor.SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformRoad);
    }
}
