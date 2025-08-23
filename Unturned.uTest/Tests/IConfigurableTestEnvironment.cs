using System;
using uTest.Environment;

namespace uTest;

public interface IConfigurableTestEnvironment
{
    /// <summary>
    /// The number of players to spawn.
    /// </summary>
    /// <remarks>Defaults to 0.</remarks>
    int PlayerCount { get; set; }

    /// <summary>
    /// Whether or not time should tick during the test.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    bool ShouldTimeProgress { get; set; }
    
    /// <summary>
    /// The in-game time at which the test will take place. Date information is ignored.
    /// </summary>
    DateTime Time { get; set; }

    /// <summary>
    /// The moon phase at which the in-game time should take place.
    /// </summary>
    MoonPhase MoonPhase { get; set; }

    /// <summary>
    /// Finalizes this configurable environment into an <see cref="ITestEnvironment"/> and applies all settings.
    /// </summary>
    ValueTask<ITestEnvironment> CreateEnvironment();

    /// <summary>
    /// Adds an action to be ran when creating the environment. Allows users to add their own environment restrictions.
    /// </summary>
    void AddCustomWorkItem(Func<IConfigurableTestEnvironment, ValueTask> action);

    /// <summary>
    /// Adds an action to be ran when creating the environment. Allows users to add their own environment restrictions.
    /// </summary>
    void AddCustomWorkItem<TState>(TState state, Func<TState, IConfigurableTestEnvironment, ValueTask> action);
}