using System;

namespace uTest;

/// <summary>
/// Defines the location and yaw angle of a player.
/// </summary>
public readonly struct PlayerTransform : IEquatable<PlayerTransform>, IFormattable
{
    /// <summary>
    /// The position of the player.
    /// </summary>
    public readonly Vector3 Position;

    /// <summary>
    /// The yaw or Y euler rotation of the player.
    /// </summary>
    public readonly float Yaw;

    /// <summary>
    /// The default spawnpoint when a map has no player spawns.
    /// </summary>
    public static PlayerTransform DefaultSpawn => new PlayerTransform(0f, 256f, 0f, 0f);

    /// <summary>
    /// Create a new <see cref="PlayerTransform"/> from a world position and yaw angle.
    /// </summary>
    /// <param name="position">The world position of the player.</param>
    /// <param name="yaw">The player's yaw, or y euler angle. The value of this angle will be clamped to within the [0, 360) range.</param>
    public PlayerTransform(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = MathHelper.ClampAngle(yaw);
    }

    /// <summary>
    /// Create a new <see cref="PlayerTransform"/> from a world position and yaw angle.
    /// </summary>
    /// <param name="x">The X component of the world position of the player.</param>
    /// <param name="y">The Y component of the world position of the player.</param>
    /// <param name="z">The Z component of the world position of the player.</param>
    /// <param name="yaw">The player's yaw, or y euler angle. The value of this angle will be clamped to within the [0, 360) range.</param>
    public PlayerTransform(float x, float y, float z, float yaw)
    {
        Position.x = x;
        Position.y = y;
        Position.z = z;
        Yaw = MathHelper.ClampAngle(yaw);
    }

    /// <summary>
    /// Creates a new <see cref="PlayerTransform"/> with the transform of the given <paramref name="player"/>.
    /// </summary>
    /// <exception cref="GameThreadException"/>
    public static PlayerTransform FromPlayer(Player player)
    {
        GameThread.Assert();

        Transform t = player.transform;
        t.GetPositionAndRotation(out Vector3 pos, out Quaternion rot);
        return new PlayerTransform(pos.x, pos.y, pos.z, rot.eulerAngles.y);
    }

    /// <summary>
    /// Creates a new <see cref="PlayerTransform"/> with the transform of the given <paramref name="spawn"/>.
    /// </summary>
    public static PlayerTransform FromPlayerSpawn(PlayerSpawnpoint spawn)
    {
        return new PlayerTransform(spawn.point, spawn.angle);
    }

    /// <summary>
    /// Creates a new <see cref="PlayerTransform"/> with the transform of a random player spawnpoint.
    /// </summary>
    /// <exception cref="GameThreadException"/>
    public static PlayerTransform FromRandomPlayerSpawn()
    {
        GameThread.Assert();
        return FromPlayerSpawn(LevelPlayers.getSpawn(false));
    }

    /// <summary>
    /// Teleports <paramref name="player"/> to this transform without collision checks.
    /// </summary>
    /// <exception cref="GameThreadException"/>
    public void TeleportPlayerUnsafe(Player player)
    {
        GameThread.Assert();

        Vector3 pos = Position;
        pos.y -= 0.5f;
        player.teleportToLocationUnsafe(pos, Yaw);
    }

    /// <summary>
    /// Teleports <paramref name="player"/> to this transform with collision checks.
    /// </summary>
    /// <returns>Whether or not the collision check passed and the player was teleported.</returns>
    /// <exception cref="GameThreadException"/>
    public bool TeleportPlayer(Player player)
    {
        GameThread.Assert();

        Vector3 pos = Position;
        pos.y -= 0.5f;
        return player.teleportToLocation(pos, Yaw);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is PlayerTransform t && Equals(t);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Position, (byte)(Yaw / 2.0));
    }

    /// <inheritdoc />
    public bool Equals(PlayerTransform other)
    {
        return other.Position.Equals(Position) && (double)Yaw == other.Yaw;
    }

    /// <summary>
    /// Checks whether or not this value is close in position and angle to <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other object to compare this object to.</param>
    /// <param name="positionTolerance">The maximum distance that can be between two positions that are considered equal.</param>
    public bool IsNearlyEqual(in PlayerTransform other, float positionTolerance = 0.001f)
    {
        return other.Position.IsNearlyEqual(Position, positionTolerance) && (byte)(Yaw / 2.0) == (byte)(other.Yaw / 2.0);
    }

    /// <summary>
    /// Checks whether or not <paramref name="left"/> is close in position and angle to <paramref name="right"/>.
    /// </summary>
    public static bool operator ==(PlayerTransform left, PlayerTransform right) => left.IsNearlyEqual(in right);

    /// <summary>
    /// Checks whether or not <paramref name="left"/> is far in position and angle from <paramref name="right"/>.
    /// </summary>
    public static bool operator !=(PlayerTransform left, PlayerTransform right) => !left.IsNearlyEqual(in right);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Position:0.##} @ {Yaw:0.#}°";
    }

    /// <inheritdoc cref="ToString(string,System.IFormatProvider)" />
    public string ToString(IFormatProvider formatProvider)
    {
        return $"{Position.ToString("0.##", formatProvider)} @ {Yaw.ToString("0.#", formatProvider)}°";
    }

    /// <inheritdoc />
    public string ToString(string format, IFormatProvider formatProvider)
    {
        return $"{Position.ToString(format, formatProvider)} @ {Yaw.ToString(format, formatProvider)}°";
    }
}