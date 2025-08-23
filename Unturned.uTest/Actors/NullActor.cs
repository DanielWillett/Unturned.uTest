using System;

namespace uTest;

/// <summary>
/// A <see cref="NullActor"/> that represents hitting other actors such as terrain, clip borders, etc.
/// </summary>
/// <remarks>Accessible through <see cref="NullActor.Instance"/>.</remarks>
public sealed class NullActor : ITestActor
{
    /// <summary>
    /// Singleton instance of <see cref="NullActor"/>.
    /// </summary>
    public static NullActor Instance { get; } = new NullActor();

    static NullActor() { }

    /// <summary>
    /// Use <see cref="NullActor.Instance"/>.
    /// </summary>
    private NullActor() { }

    /// <inheritdoc />
    public bool Equals(ITestActor? other)
    {
        return other is NullActor;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is NullActor;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return 0;
    }

    public static bool operator ==(NullActor? left, NullActor? right)
    {
        return left is null ? right is null : right is not null;
    }
    public static bool operator !=(NullActor? left, NullActor? right)
    {
        return left is null ? right is not null : right is null;
    }

    /// <summary>
    /// Always equal to "null".
    /// </summary>
    public string DisplayName => "null";

    /// <summary>
    /// Always <see langword="null"/>.
    /// </summary>
    NetId? ITestActor.NetId => null;

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    Vector3 ITestActor.Position
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    Quaternion ITestActor.Rotation
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    Vector3 ITestActor.Scale
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    void ITestActor.SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }

    /// <summary>
    /// Always equal to <see langword="false"/>.
    /// </summary>
    bool ITestActor.IsAlive => false;

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    double ITestActor.Health
    {
        get => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
        set => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    double ITestActor.MaximumHealth => throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    void ITestActor.Kill()
    {
        throw new NotSupportedException(Properties.Resources.NotSupportedExceptionNullActor);
    }
}