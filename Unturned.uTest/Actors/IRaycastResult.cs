using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace uTest;

/// <summary>
/// Raycast information.
/// </summary>
public interface IRaycastResult
{
    /// <summary>
    /// If the raycast hit anything. <see cref="Actor"/> will not be <see langword="null"/> if this is <see langword="true"/>.
    /// </summary>
    bool IsHit { [MemberNotNullWhen(true, nameof(Actor))] get; }

    /// <summary>
    /// The actor that was hit. If the hit object isn't an actor (such as terrain or the level clip), this will be the <see cref="NullActor"/>.
    /// </summary>
    ITestActor? Actor { get; }

    /// <summary>
    /// Distance away from <see cref="Origin"/> the ray hit.
    /// </summary>
    float Distance { get; }

    /// <summary>
    /// Exact position where the ray hit.
    /// </summary>
    Vector3 Point { get; }

    /// <summary>
    /// Where the ray started from.
    /// </summary>
    Vector3 Origin { get; }

    /// <summary>
    /// The forward vector of the ray.
    /// </summary>
    Vector3 Direction { get; }

    /// <summary>
    /// Extra information provided by Unity about the raycast.
    /// </summary>
    ref RaycastHit PhysicsHit { get; }
}

/// <summary>
/// Raycast information with extra information about a specific type of actor.
/// </summary>
public interface IRaycastResult<THitInfo> : IRaycastResult where THitInfo : struct, IHitInfo
{
    /// <summary>
    /// Extra information about a specific type of actor.
    /// </summary>
    ref readonly THitInfo Info { get; }
}

internal class RaycastResult : IRaycastResult
{
    private RaycastHit _hit;

    public bool IsHit { [MemberNotNullWhen(true, nameof(Actor)), MemberNotNullWhen(true, nameof(RootTransform))] get; protected init; }
    public ITestActor? Actor { get; protected init; }
    public float Distance { get; protected init; }
    public Vector3 Point { get; protected init; }
    public Vector3 Origin { get; protected init; }
    public Vector3 Direction { get; protected init; }
    public ref RaycastHit PhysicsHit => ref _hit;
    public Transform? RootTransform { get; protected init; }
    public RaycastResult(ref Ray ray, float maxDistance, ref RaycastHit hit)
    {
        _hit = hit;
        Origin = ray.origin;
        Direction = ray.direction;

        if (hit.transform != null)
            return;

        if (!float.IsInfinity(maxDistance) && !float.IsNaN(maxDistance))
        {
            Point = ray.origin + ray.direction * maxDistance;
        }
    }
}

internal class RaycastResult<THitInfo> : RaycastResult, IRaycastResult<THitInfo> where THitInfo : struct, IHitInfo
{
    private readonly THitInfo _info;

    public ref readonly THitInfo Info => ref _info;

    public RaycastResult(ref Ray ray, float maxDistance, ref RaycastHit hit)
        : base(ref ray, maxDistance, ref hit)
    {
        Transform? t = hit.transform;
        if (t == null)
            return;

        IsHit = true;
        Distance = hit.distance;
        Point = hit.point;
        RootTransform = t;

        // isn't this beautiful
        string tag = t.tag;
        if (tag.Equals("Barricade", StringComparison.Ordinal))
        {
            BarricadeDrop? drop = BarricadeManager.FindBarricadeByRootTransform(DamageTool.getBarricadeRootTransform(t));
            if (drop?.asset != null)
            {
                BarricadeData data = drop.GetServersideData();
                _info = CastInfo(new BarricadeHit
                {
                    Drop = drop,
                    Data = data,
                    Asset = drop.asset,
                    Item = data.barricade
                });
                Actor = new BarricadeActor(drop, data);
                RootTransform = drop.model;
            }
        }
        else if (tag.Equals("Structure", StringComparison.Ordinal))
        {
            StructureDrop? drop = StructureManager.FindStructureByRootTransform(DamageTool.getStructureRootTransform(t));
            if (drop?.asset != null)
            {
                StructureData data = drop.GetServersideData();
                _info = CastInfo(new StructureHit
                {
                    Drop = drop,
                    Data = data,
                    Asset = drop.asset,
                    Item = data.structure
                });
                Actor = new StructureActor(drop, data);
                RootTransform = drop.model;
            }
        }
        else if (tag.Equals("Resource", StringComparison.Ordinal))
        {
            Vector3 pos = t.position;
            double xFull = Math.Floor((pos.x + 4096f) / Regions.REGION_SIZE);
            double yFull = Math.Floor((pos.z + 4096f) / Regions.REGION_SIZE);
            if (xFull is > byte.MaxValue or < 0 || yFull is > byte.MaxValue or < 0)
            {
                byte x = (byte)xFull;
                byte y = (byte)yFull;
                ResourceSpawnpoint? spawnpoint = LevelGround.GetTreesOrNullInRegion(x, y)?.Find(x => x.model == t);
                if (spawnpoint?.asset != null)
                {
                    _info = CastInfo(new ResourceHit
                    {
                        Spawnpoint = spawnpoint,
                        Asset = spawnpoint.asset
                    });
                    Actor = new ResourceActor(spawnpoint);
                    RootTransform = spawnpoint.model;
                }
            }
        }
        else if (tag.Equals("Enemy", StringComparison.Ordinal))
        {
            Player? player = DamageTool.getPlayer(t);
            if (player != null)
            {
                _info = CastInfo(new PlayerHit
                {
                    Player = player,
                    SteamPlayer = player.channel.owner,
                    Limb = DamageTool.getLimb(t)
                });
                Actor = PlayerActor.Create(player);
                RootTransform = player.transform;
            }
        }
        else if (tag.Equals("Zombie", StringComparison.Ordinal))
        {
            Zombie? zombie = DamageTool.getZombie(t);
            if (zombie != null)
            {
                _info = CastInfo(new ZombieHit
                {
                    Zombie = zombie,
                    Limb = t.gameObject.layer == LayerMasks.ENTITY ? DamageTool.getLimb(t) : null,
                    Asset = zombie.difficulty
                });
                Actor = new ZombieActor(zombie);
                RootTransform = zombie.transform;
            }
        }
        else if (tag.Equals("Animal", StringComparison.Ordinal))
        {
            Animal? animal = DamageTool.getAnimal(t);
            if (animal != null && animal.asset != null)
            {
                _info = CastInfo(new AnimalHit
                {
                    Animal = animal,
                    Limb = t.gameObject.layer == LayerMasks.ENTITY ? DamageTool.getLimb(t) : null,
                    Asset = animal.asset
                });
                Actor = new AnimalActor(animal);
                RootTransform = animal.transform;
            }
        }
        else if (Dedicator.isStandaloneDedicatedServer && tag.Equals("Agent", StringComparison.Ordinal))
        {
            Animal? animal = DamageTool.getAnimal(t);
            if (animal != null && animal.asset != null)
            {
                _info = CastInfo(new AnimalHit
                {
                    Animal = animal,
                    Limb = null,
                    Asset = animal.asset
                });
                Actor = new AnimalActor(animal);
                RootTransform = animal.transform;
            }
            else
            {
                Zombie? zombie = DamageTool.getZombie(t);
                if (zombie != null)
                {
                    _info = CastInfo(new ZombieHit
                    {
                        Zombie = zombie,
                        Limb = null,
                        Asset = zombie.difficulty
                    });
                    Actor = new ZombieActor(zombie);
                    RootTransform = zombie.transform;
                }
            }
        }
        else if (tag.Equals("Vehicle", StringComparison.Ordinal))
        {
            InteractableVehicle? vehicle = DamageTool.getVehicle(t);
            if (vehicle != null && vehicle.asset != null)
            {
                _info = CastInfo(new VehicleHit
                {
                    Vehicle = vehicle,
                    Asset = vehicle.asset
                });
                Actor = new VehicleActor(vehicle);
                RootTransform = vehicle.transform;
            }
        }
        else if (t.GetComponentInParent<InteractableObjectRubble>() is { } rubbleObject && rubbleObject != null)
        {
            RootTransform = rubbleObject.transform;
            if (ObjectManager.tryGetRegion(RootTransform, out byte x, out byte y, out ushort index))
            {
                LevelObject obj = LevelObjects.objects[x, y][index];
                if (obj.asset != null)
                {
                    byte section = rubbleObject.getSection(hit.collider.transform);
                    _info = CastInfo(new ObjectHit
                    {
                        Object = obj,
                        Asset = obj.asset,
                        Section = section
                    });
                    Actor = new RubbleObjectActor(obj, rubbleObject, section);
                }
            }
        }
        else if (tag.Equals("Large", StringComparison.Ordinal) || tag.Equals("Medium", StringComparison.Ordinal) || tag.Equals("Small", StringComparison.Ordinal))
        {
            if (ObjectManager.tryGetRegion(RootTransform, out byte x, out byte y, out ushort index))
            {
                LevelObject obj = LevelObjects.objects[x, y][index];
                if (obj.asset != null)
                {
                    _info = CastInfo(new ObjectHit
                    {
                        Object = obj,
                        Asset = obj.asset
                    });
                    Actor = new ObjectActor(obj);
                }
            }
        }
        else if (tag.Equals("Environment", StringComparison.Ordinal))
        {
            Transform road = t.root;
            for (int i = 0; i < ushort.MaxValue; ++i)
            {
                Road? rd = LevelRoads.getRoad(i);
                if (rd == null)
                    break;

                if (road != rd.road)
                    continue;

                RoadAsset? asset = rd.GetRoadAsset();
                byte legacy = byte.MaxValue;
                if (asset == null)
                    legacy = rd.material;
                _info = CastInfo(new RoadHit
                {
                    HasRoadAsset = asset != null,
                    Asset = asset,
                    LegacyMaterialIndex = legacy,
                    Road = rd
                });
                Actor = new RoadActor(rd, asset, legacy);
                break;
            }
        }

        if (Actor != null)
            return;

        _info = default;
        Actor = NullActor.Instance;
    }

    private static THitInfo CastInfo<TFrom>(TFrom value)
    {
        if (typeof(THitInfo) != typeof(TFrom))
        {
            return default;
        }

        return Unsafe.As<TFrom, THitInfo>(ref value);
    }
}

public interface IHitInfo;

public readonly struct BarricadeHit : IHitInfo
{
    public required BarricadeDrop Drop { get; init; }
    public required BarricadeData Data { get; init; }
    public required Barricade Item { get; init; }
    public required ItemBarricadeAsset Asset { get; init; }
}
public readonly struct StructureHit : IHitInfo
{
    public required StructureDrop Drop { get; init; }
    public required StructureData Data { get; init; }
    public required Structure Item { get; init; }
    public required ItemStructureAsset Asset { get; init; }
}
public readonly struct ResourceHit : IHitInfo
{
    public required ResourceSpawnpoint Spawnpoint { get; init; }
    public required ResourceAsset Asset { get; init; }
}
public readonly struct PlayerHit : IHitInfo
{
    public required Player Player { get; init; }
    public required SteamPlayer SteamPlayer { get; init; }
    public required ELimb? Limb { get; init; }
}
public readonly struct ZombieHit : IHitInfo
{
    public required Zombie Zombie { get; init; }
    public required ELimb? Limb { get; init; }
    public required ZombieDifficultyAsset? Asset { get; init; }
}
public readonly struct AnimalHit : IHitInfo
{
    public required Animal Animal { get; init; }
    public required ELimb? Limb { get; init; }
    public required AnimalAsset Asset { get; init; }
}
public readonly struct VehicleHit : IHitInfo
{
    public required InteractableVehicle Vehicle { get; init; }
    public required VehicleAsset Asset { get; init; }
}
public readonly struct ObjectHit : IHitInfo
{
    public required LevelObject Object { get; init; }
    public required ObjectAsset Asset { get; init; }
    public byte? Section { get; init; }
}
public readonly struct RoadHit : IHitInfo
{
    public required Road Road { get; init; }
    public required bool HasRoadAsset { [MemberNotNullWhen(true, nameof(Asset))] get; init; }
    public required RoadAsset? Asset { get; init; }
    public required byte LegacyMaterialIndex { get; init; }
}