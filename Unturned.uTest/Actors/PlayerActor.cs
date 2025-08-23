using System;

namespace uTest;

internal class PlayerActor : ITestPlayer
{
    internal const float ReachDistance = 4;

    private static readonly Vector3 TeleportOffset = new Vector3(0f, 0.5f, 0f);

    private readonly Player _player;
    private CSteamID _steam64;

    public ITestPlayerLook Look { get; }
    public ITestPlayerInventory Inventory { get; }

    public PlayerActor(Player player)
    {
        _player = player;
        _steam64 = _player.channel.owner.playerID.steamID;
        Look = new PlayerActorLook(this);
        Inventory = new PlayerActorInventory(this);
    }

    /// <summary>
    /// Checks if a player is a bot/dummy player.
    /// </summary>
    public bool IsBot => _steam64.GetEAccountType() != EAccountType.k_EAccountTypeIndividual;

    /// <summary>
    /// The Steam64 ID of this player, may not be a valid Steam64 ID for bot players.
    /// </summary>
    public ref readonly CSteamID Steam64 => ref _steam64;

    /// <inheritdoc />
    public string DisplayName => _player.channel.owner.playerID.characterName;

    /// <inheritdoc />
    public NetId? NetId
    {
        get
        {
            NetId nId = _player.GetNetId();
            return nId.IsNull() ? null : nId;
        }
    }

    /// <inheritdoc />
    public Vector3 Position
    {
        get
        {
            return GameThread.IsCurrent
                ? _player.transform.position
                : GameThread.RunAndWait(_player, static player => player.transform.position);
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait(
                    (me: this, position: value),
                    static args => args.me.Position = args.position
                );
                return;
            }

            EventToggle.Invoke((pl: _player, position: value), static args =>
            {
                args.pl.teleportToLocationUnsafe(args.position - TeleportOffset, args.pl.look.yaw);
            });
        }
    }

    /// <inheritdoc />
    public Quaternion Rotation
    {
        get
        {
            return GameThread.IsCurrent
                ? Quaternion.Euler(_player.look.pitch, _player.look.yaw, 0f)
                : GameThread.RunAndWait(_player, static player => Quaternion.Euler(player.look.pitch, player.look.yaw, 0f));
        }
        set
        {
            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait(
                    (me: this, rotation: value),
                    static args => args.me.Rotation = args.rotation
                );
                return;
            }

            EventToggle.Invoke((pl: _player, yaw: value.eulerAngles.y), static args =>
            {
                args.pl.teleportToLocationUnsafe(args.pl.transform.position - TeleportOffset, args.yaw);
            });
        }
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get => Vector3.one;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetScalePlayer);
    }

    /// <inheritdoc />
    public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        if (!GameThread.IsCurrent)
        {
            GameThread.RunAndWait(
                (me: this, position, rotation),
                static args => args.me.SetPositionAndRotation(args.position, args.rotation)
            );
            return;
        }

        EventToggle.Invoke((pl: _player, pos: position, yaw: rotation.eulerAngles.y), static args =>
        {
            args.pl.teleportToLocationUnsafe(args.pos, args.yaw);
        });
    }

    /// <inheritdoc />
    public bool IsAlive => _player.life.IsAlive;

    /// <inheritdoc />
    public double Health
    {
        get => _player.life.health;
        set
        {
            if (value is > 100 or < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (!GameThread.IsCurrent)
            {
                GameThread.RunAndWait((me: this, hp: value), static args => args.me.Health = args.hp);
                return;
            }
            
            byte health = (byte)Math.Round(value);
            byte currentHealth = _player.life.health;
            if (currentHealth == health)
                return;

            if (health > currentHealth)
            {
                _player.life.askHeal((byte)(health - currentHealth), false, false);
            }
            else
            {
                _player.life.askDamage(
                    (byte)(currentHealth - health),
                    Vector3.zero,
                    EDeathCause.KILL,
                    ELimb.SPINE,
                    CSteamID.Nil,
                    out _,
                    false,
                    ERagdollEffect.NONE,
                    bypassSafezone: true,
                    canCauseBleeding: false
                );
            }
        }
    }

    public float Yaw => _player.look.yaw;
    public float Pitch => _player.look.pitch;

    /// <inheritdoc />
    public void Kill()
    {
        AssertNotDead();
        Health = 0;
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is PlayerActor pl && pl._steam64.m_SteamID == _steam64.m_SteamID;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is PlayerActor pl && pl._steam64.m_SteamID == _steam64.m_SteamID;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return unchecked ( (int)_steam64.GetAccountID().m_AccountID );
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Player: {Steam64} [{_player.channel.owner.playerID.playerName}]";
    }

    public static bool operator ==(PlayerActor left, PlayerActor right)
    {
        return left._steam64.m_SteamID == right._steam64.m_SteamID;
    }
    public static bool operator !=(PlayerActor left, PlayerActor right)
    {
        return left._steam64.m_SteamID == right._steam64.m_SteamID;
    }

    private void AssertNotDead()
    {
        if (_player.life.isDead)
            throw new ActorDestroyedException(this);
    }

    private class PlayerActorLook(PlayerActor player) : ITestPlayerLook
    {
        public readonly PlayerActor Player = player;
        private readonly PlayerLook _look = player._player.look;

        public Vector3 Origin => _look.aim.position;
        public Vector3 Forward => _look.aim.forward;
        public Quaternion Rotation => _look.aim.rotation;

        /// <inheritdoc />
        public IRaycastResult Raycast(ActorMask mask, float maxDistance = ReachDistance, bool collideWithTriggers = false)
        {
            Ray ray = new Ray(_look.aim.position, _look.aim.forward);

            Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxDistance,
                mask.RayMask,
                collideWithTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore
            );

            return new RaycastResult(ref ray, maxDistance, ref hit);
        }

        /// <inheritdoc />
        public IRaycastResult<THitInfo> Raycast<THitInfo>(ActorMask mask, float maxDistance = ReachDistance, bool collideWithTriggers = false)
            where THitInfo : struct, IHitInfo
        {
            Ray ray = new Ray(_look.aim.position, _look.aim.forward);


            Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxDistance,
                mask.RayMask | ActorMask.FromHitInfoType<THitInfo>().RayMask,
                collideWithTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore
            );

            return new RaycastResult<THitInfo>(ref ray, maxDistance, ref hit);
        }

        /// <inheritdoc />
        public IRaycastResult Raycast(float maxDistance = ReachDistance, bool collideWithTriggers = false)
        {
            return Raycast(ActorMask.Default, maxDistance, collideWithTriggers);
        }

        /// <inheritdoc />
        public IRaycastResult<THitInfo> Raycast<THitInfo>(float maxDistance = ReachDistance, bool collideWithTriggers = false)
            where THitInfo : struct, IHitInfo
        {
            return Raycast<THitInfo>(ActorMask.Default, maxDistance, collideWithTriggers);
        }
    }

    private class PlayerActorInventory(PlayerActor player) : ITestPlayerInventory
    {
        public readonly PlayerActor Player = player;
        private readonly PlayerInventory _inventory = player._player.inventory;
        private readonly PlayerClothing _clothing = player._player.clothing;
        private readonly PlayerCrafting _crafting = player._player.crafting;
        private readonly PlayerEquipment _equipment = player._player.equipment;
    }

    double ITestActor.MaximumHealth => 100d;
}