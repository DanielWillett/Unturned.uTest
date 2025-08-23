using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// An actor for a specific rubble section in a <see cref="LevelObject"/>.
/// </summary>
/// <remarks>Inherits <see cref="ObjectActor"/>.</remarks>
public class RubbleObjectActor : ObjectActor
{
    /// <summary>
    /// The section this actor refers to.
    /// </summary>
    public byte SectionIndex { get; }

    /// <summary>
    /// The rubble group this actor refers to.
    /// </summary>
    public InteractableObjectRubble RubbleGroup { get; }

    /// <summary>
    /// Information about the section this actor refers to.
    /// </summary>
    public RubbleInfo Section { get; }

    /// <summary>
    /// Create a new <see cref="RubbleObjectActor"/>
    /// </summary>
    /// <param name="levelObject">The object this actor refers to.</param>
    /// <param name="rubbleGroup">The <see cref="InteractableObjectRubble"/> component on <paramref name="levelObject"/>.</param>
    /// <param name="section">Section index in <paramref name="rubbleGroup"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="levelObject"/> or <paramref name="rubbleGroup"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> isn't a valid section in <paramref name="rubbleGroup"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rubbleGroup"/> doesn't belong to the same object as <paramref name="levelObject"/>.</exception>
    public RubbleObjectActor(LevelObject levelObject, InteractableObjectRubble rubbleGroup, byte section) : base(levelObject)
    {
        if (rubbleGroup is null)
            throw new ArgumentNullException(nameof(rubbleGroup));

        if (section >= rubbleGroup.getSectionCount())
            throw new ArgumentOutOfRangeException(nameof(section));

        if (levelObject.transform != rubbleGroup.transform)
            throw new ArgumentException(Properties.Resources.LevelObjectInconsistantRubble, nameof(rubbleGroup));

        Section = rubbleGroup.getSectionInfo(section);
        SectionIndex = section;
        RubbleGroup = rubbleGroup;
    }

    /// <inheritdoc />
    public override bool Equals(ITestActor? other)
    {
        if (other is RubbleObjectActor actor)
            return actor.Object == Object && actor.SectionIndex == SectionIndex;

        if (other is not ObjectActor oActor)
            return false;

        return oActor.Object == Object;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ObjectActor actor && Equals(actor);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int shift = SectionIndex % 32;
        int hash = unchecked( (int)Object.instanceID );
        if (shift == 0)
            return hash;

        return (hash << shift) | (hash >>> (32 - shift));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Rubble: {DisplayName} (#{Object.instanceID}, Section {SectionIndex})";
    }

    /// <inheritdoc />
    public override bool IsAlive => base.IsAlive && !Section.isDead;

    /// <inheritdoc />
    public override double Health
    {
        get => Section.health;
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

            if (Transform is null || Transform == Object.placeholderTransform)
                throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectNotSpawned);

            if (!ObjectManager.tryGetRegion(Transform, out _, out _, out _))
            {
                throw new ActorOutOfBoundsException(this);
            }
            
            ushort health = (ushort)Math.Round(value);
            ushort currentHealth = Section.health;
            if (health == currentHealth)
                return;

            if (health > currentHealth)
            {
                Section.health = health;
            }
            else
            {
                float hp = currentHealth - health;
                EventToggle.Invoke((me: this, hp), static args => ObjectManager.damage(args.me.Transform, Vector3.zero, args.me.SectionIndex, args.hp, 1f, out _, out _, trackKill: false));
            }
        }
    }

    /// <inheritdoc />
    public override double MaximumHealth => Object.asset?.rubbleHealth ?? 0;

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator RubbleInfo?(RubbleObjectActor? actor) => actor?.Section;

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator InteractableObjectRubble?(RubbleObjectActor? actor) => actor?.RubbleGroup;
}
