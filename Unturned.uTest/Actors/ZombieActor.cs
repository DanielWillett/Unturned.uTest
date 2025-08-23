using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace uTest;

/// <summary>
/// An actor for a <see cref="SDG.Unturned.Zombie"/>. Note that zombies always exist but can become alive or dead at any point.
/// </summary>
public class ZombieActor(Zombie zombie) : ITestActor
{
    private static FieldInfo? _zombieHealthField;

    /// <summary>
    /// The underlying <see cref="SDG.Unturned.Zombie"/> this actor represents.
    /// </summary>
    public Zombie Zombie { get; } = zombie;

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            if (Zombie.type < LevelZombies.tables.Count)
            {
                string name = LevelZombies.tables[Zombie.type].name;
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return Zombie.speciality.ToString();
        }
    }

    /// <inheritdoc />
    public Vector3 Position
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait(this, static me => me.Position);
            }

            AssertNotDead();
            return Zombie.transform.position;
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
            AssertNotDead();
            Zombie.transform.position = value;
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

            AssertNotDead();
            return Zombie.transform.rotation;
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
            AssertNotDead();
            Zombie.transform.rotation = Quaternion.Euler(0f, value.eulerAngles.y, 0f);
        }
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get => Vector3.one;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetScaleZombie);
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
        AssertNotDead();
        Zombie.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
    }

    /// <inheritdoc />
    public bool IsAlive => !Zombie.isDead;

    /// <inheritdoc />
    public double Health
    {
        get
        {
            AuthorityHelper.AssertServer(this);
            return Zombie.GetHealth();
        }
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
            AssertNotDead();

            float newHealth = (float)value;
            float currentHealth = Zombie.GetHealth();

            if (currentHealth >= newHealth)
            {
                double dmg = Math.Round(currentHealth - newHealth);
                if (dmg <= 0)
                    return;
                if (dmg > ushort.MaxValue)
                    dmg = ushort.MaxValue;
                ushort damage = (ushort)dmg;
                Zombie.askDamage(damage, Vector3.zero, out _, out _, trackKill: false, dropLoot: false, stunOverride: EZombieStunOverride.Never);
            }
            else
            {
                double newHealthRounded = Math.Round(newHealth);
                if (newHealthRounded > ushort.MaxValue)
                    newHealthRounded = ushort.MaxValue;

                if (_zombieHealthField == null)
                {
                    _zombieHealthField ??= typeof(Zombie).GetField("health", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (_zombieHealthField == null || _zombieHealthField.FieldType != typeof(ushort))
                    {
                        _zombieHealthField = null;
                        throw new NotSupportedException(string.Format(Properties.Resources.ReflectionFailureWithMember, "Zombie.health"));
                    }
                }

                _zombieHealthField.SetValue(Zombie, (ushort)newHealthRounded);
            }
        }
    }

    /// <inheritdoc />
    public double MaximumHealth
    {
        get
        {
            AuthorityHelper.AssertServer(this);
            return Zombie.GetMaxHealth();
        }
    }

    /// <inheritdoc />
    public void Kill()
    {
        Health = 0;
    }

    private void AssertNotDead()
    {
        if (Zombie.isDead)
            throw new ActorDestroyedException(this);
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is ZombieActor actor && actor.Zombie == Zombie;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ZombieActor actor && Zombie == actor.Zombie;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Zombie.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Zombie: {DisplayName}";
    }

    public static bool operator ==(ZombieActor? a, ZombieActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(ZombieActor? a, ZombieActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator Zombie?(ZombieActor? actor) => actor?.Zombie;

    NetId? ITestActor.NetId => null;
}
