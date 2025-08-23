using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace uTest;

/// <summary>
/// An actor for an <see cref="SDG.Unturned.Animal"/>. Note that animals always exist but can become alive or dead at any point.
/// </summary>
public class AnimalActor(Animal animal) : ITestActor
{
    private static FieldInfo? _animalHealthField;

    /// <summary>
    /// The underlying <see cref="SDG.Unturned.Animal"/> this actor represents.
    /// </summary>
    public Animal Animal { get; } = animal;

    /// <inheritdoc />
    public string DisplayName => Animal.asset?.animalName ?? Animal.asset?.name ?? "Animal";

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
            return Animal.transform.position;
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
            Animal.transform.position = value;
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
            return Animal.transform.rotation;
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
            Animal.transform.rotation = Quaternion.Euler(0f, value.eulerAngles.y, 0f);
        }
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get => Vector3.one;
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetScaleAnimal);
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
        Animal.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
    }

    /// <inheritdoc />
    public bool IsAlive => !Animal.isDead;

    /// <inheritdoc />
    public double Health
    {
        get
        {
            AuthorityHelper.AssertServer(this);
            return Animal.GetHealth();
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

            float currentHealth = Animal.GetHealth();

            if (currentHealth >= value)
            {
                double dmg = Math.Round(currentHealth - value);
                if (dmg <= 0)
                    return;
                if (dmg > ushort.MaxValue)
                    dmg = ushort.MaxValue;
                ushort damage = (ushort)dmg;
                Animal.askDamage(damage, Vector3.zero, out _, out _, trackKill: false, dropLoot: false);
            }
            else
            {
                double newHealthRounded = Math.Round(value);
                if (newHealthRounded > ushort.MaxValue)
                    newHealthRounded = ushort.MaxValue;

                if (_animalHealthField == null)
                {
                    _animalHealthField = typeof(Animal).GetField("health", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (_animalHealthField == null || _animalHealthField.FieldType != typeof(ushort))
                    {
                        _animalHealthField = null;
                        throw new NotSupportedException(string.Format(Properties.Resources.ReflectionFailureWithMember, "Animal.health"));
                    }
                }

                _animalHealthField.SetValue(Animal, (ushort)newHealthRounded);
            }
        }
    }

    /// <inheritdoc />
    public double MaximumHealth => Animal.asset?.health ?? 0;

    /// <inheritdoc />
    public void Kill()
    {
        Health = 0;
    }

    private void AssertNotDead()
    {
        if (Animal.isDead)
            throw new ActorDestroyedException(this);
    }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is AnimalActor actor && actor.Animal == Animal;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AnimalActor actor && Animal == actor.Animal;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Animal.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Animal: {DisplayName}";
    }

    public static bool operator ==(AnimalActor? a, AnimalActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(AnimalActor? a, AnimalActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator Animal?(AnimalActor? actor) => actor?.Animal;

    NetId? ITestActor.NetId => null;
}
