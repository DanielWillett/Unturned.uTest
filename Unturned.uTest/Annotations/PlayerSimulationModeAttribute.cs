using System;

namespace uTest;

/// <inheritdoc cref="PlayerSimulationMode"/>
/// <param name="mode">Describes how players are simulated. If any test in a run requires full simulation, all tests will use full simulation.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class PlayerSimulationModeAttribute(PlayerSimulationMode mode) : Attribute
{
    /// <inheritdoc cref="PlayerSimulationMode"/>
    public PlayerSimulationMode Mode { get; } = mode;
}

/// <summary>
/// Describes how players are simulated. If any test in a run requires full simulation, all tests will use full simulation.
/// </summary>
public enum PlayerSimulationMode
{
    /// <summary>
    /// Partially simulates a new player by creating a dummy managed by the server. May not perfectly mimic normal players in some cases, but is much less resource-intensive.
    /// </summary>
    Simulated,

    /// <summary>
    /// Fully simulates a new player by starting up an instance of the Unturned client and connecting it to the server for each player.
    /// </summary>
    Remote
}