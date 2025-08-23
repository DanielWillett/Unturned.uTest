using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// An actor for a <see cref="LevelObject"/>.
/// </summary>
public class ObjectActor : ITestActor
{
    /// <summary>
    /// The underlying <see cref="LevelObject"/> this actor represents.
    /// </summary>
    public LevelObject Object { get; }

    /// <summary>
    /// The transform of this object, falling back to a placeholder transform if the placeholder transform if the asset is missing.
    /// </summary>
    public Transform? Transform { get; }

    /// <summary>
    /// An actor for an <see cref="LevelObject"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public ObjectActor(LevelObject levelObject)
    {
        Object = levelObject ?? throw new ArgumentNullException(nameof(levelObject));
        if (levelObject.transform != null)
        {
            Transform = levelObject.transform;
            return;
        }

        if (levelObject.placeholderTransform != null)
        {
            Transform = levelObject.placeholderTransform;
        }
    }

    /// <inheritdoc />
    public NetId? NetId
    {
        get
        {
            Transform t = Object.transform;
            if (t is null)
                return null;

            NetId nId = NetIdRegistry.GetTransformNetId(t);
            return nId.IsNull() ? null : nId;
        }
    }

    /// <inheritdoc />
    public string DisplayName => Object.asset?.objectName ?? Object.asset?.name ?? "Level Object";

    /// <inheritdoc />
    public Vector3 Position
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait(this, static me => me.Position);
            }

            if (Transform is null)
                throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectNotSpawned);

            try
            {
                return Transform.position;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformObject);
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

            if (Transform is null)
                throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectNotSpawned);

            try
            {
                return Transform.rotation;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformObject);
    }

    /// <inheritdoc />
    public Vector3 Scale
    {
        get
        {
            if (!GameThread.IsCurrent)
            {
                return GameThread.RunAndWait(this, static me => me.Scale);
            }

            if (Transform is null)
                throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectNotSpawned);

            try
            {
                return Transform.localScale;
            }
            catch (NullReferenceException)
            {
                throw new ActorDestroyedException(this);
            }
        }
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformObject);
    }

    /// <inheritdoc />
    void ITestActor.SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionSetTransformObject);
    }

    /// <inheritdoc />
    public virtual bool IsAlive => true;

    /// <inheritdoc />
    public virtual double Health
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectHealth);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectHealth);
    }

    /// <inheritdoc />
    public virtual double MaximumHealth => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectHealth);

    /// <inheritdoc />
    public void Kill()
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionObjectHealth);
    }
    
    /// <inheritdoc />
    public virtual bool Equals(ITestActor? other)
    {
        return other is ObjectActor actor && actor.Object == Object;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ObjectActor actor && Object == actor.Object;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return unchecked ( (int)Object.instanceID );
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Object: {DisplayName} (#{Object.instanceID})";
    }

    public static bool operator ==(ObjectActor? a, ObjectActor? b)
    {
        if (a is null)
            return b is null;

        return a.Equals(b);
    }

    public static bool operator !=(ObjectActor? a, ObjectActor? b)
    {
        return !(a == b);
    }

    [return: NotNullIfNotNull(nameof(actor))]
    public static implicit operator LevelObject?(ObjectActor? actor) => actor?.Object;
}
