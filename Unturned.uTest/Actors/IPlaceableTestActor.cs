using System;

namespace uTest;

/// <summary>
/// A test actor that's either a barricade or structure.
/// </summary>
public interface IPlaceableTestActor : ITestActor
{
    /// <summary>
    /// The asset of the barricade or structure.
    /// </summary>
    ItemPlaceableAsset Asset { get; }

    /// <summary>
    /// If this placeable is a structure or barricade.
    /// </summary>
    PlaceableType Type { get; }

    /// <summary>
    /// The instance ID of this placeable. Note that structures and barricades can have overlapping instance IDs.
    /// </summary>
    uint InstanceId { get; }

    /// <summary>
    /// Gets the <see cref="BarricadeDrop"/> or <see cref="StructureDrop"/> for this placeable.
    /// </summary>
    /// <typeparam name="T"><see cref="BarricadeDrop"/> or <see cref="StructureDrop"/></typeparam>
    /// <exception cref="InvalidCastException">The requested type is not the correct drop type.</exception>
    T GetDrop<T>() where T : class;

    /// <summary>
    /// Gets the <see cref="BarricadeData"/> or <see cref="StructureData"/> for this placeable.
    /// </summary>
    /// <typeparam name="T"><see cref="BarricadeData"/> or <see cref="StructureData"/></typeparam>
    /// <exception cref="InvalidCastException">The requested type is not the correct drop type.</exception>
    /// <exception cref="ActorMissingAuthorityException">Not on the server.</exception>
    T GetServersideData<T>() where T : class;
}

/// <summary>
/// Type of placeable; a barricade or structure.
/// </summary>
public enum PlaceableType
{
    /// <summary>
    /// Can be placed on vehicles, referred to as 'not planted', or anywhere in the world, referred to as 'planted'.
    /// </summary>
    Barricade,

    /// <summary>
    /// Can only be placed on the ground and can snap to each other.
    /// </summary>
    Structure
}