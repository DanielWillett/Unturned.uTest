using System;
using uTest.Logging;

namespace uTest.Compat;

/// <summary>
/// Defines a task that must complete before tests can be ran.
/// This is used to, for example, wait for plugins to load before running tests.
/// </summary>
/// <remarks>Implementations must define either a parameterless constructor or a constructor with a single <see cref="ILogger"/> parameter.</remarks>
public interface IStartupHook
{
    /// <summary>
    /// Wait for the startup hook to be ready.
    /// </summary>
    /// <returns>
    /// A task that completes when tests can be ran and contains any types of child startup hooks that may be detected.
    /// <para>
    /// If the type of the current hook is returned, this function will be invoked again later.
    /// </para>
    /// </returns>
    Task<IList<StartupHook>> WaitAsync(CancellationToken token);
}

#nullable disable

/// <summary>
/// Data structure returned by <see cref="IStartupHook.WaitAsync"/> that can either represent an implementation of <see cref="IStartupHook"/> or the type of one.
/// </summary>
public readonly struct StartupHook
{
    /// <summary>
    /// An implementation of <see cref="IStartupHook"/> to invoke on startup.
    /// </summary>
    public IStartupHook Hook { get; }

    /// <summary>
    /// The type of a <see cref="IStartupHook"/> to invoke on startup.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Create a <see cref="StartupHook"/> that represents an <see cref="IStartupHook"/> implementation.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public StartupHook(IStartupHook hook)
    {
        Hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    /// <summary>
    /// Create a <see cref="StartupHook"/> that represents an <see cref="IStartupHook"/> type.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"><paramref name="type"/> is abstract or doesn't implement <see cref="IStartupHook"/>.</exception>
    public StartupHook(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (!typeof(IStartupHook).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException("Expected a non-abstract type that implements IStartupHook.");

        Type = type;
    }
}