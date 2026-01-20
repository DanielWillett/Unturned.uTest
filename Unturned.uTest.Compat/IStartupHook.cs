using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

public readonly struct StartupHook
{
    public IStartupHook Hook { get; }
    public Type Type { get; }

    public StartupHook(IStartupHook hook)
    {
        Hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    public StartupHook(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (!typeof(IStartupHook).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException("Expected a non-abstract type that implements IStartupHook.");

        Type = type;
    }
}