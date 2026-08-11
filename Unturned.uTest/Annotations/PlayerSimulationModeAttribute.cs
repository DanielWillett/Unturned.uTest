using JetBrains.Annotations;
using System;

namespace uTest;

/// <inheritdoc cref="PlayerSimulationMode"/>
/// <param name="mode">Describes how players are simulated. If any test in a run requires full simulation, all tests will use full simulation.</param>
[UsedImplicitly]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class PlayerSimulationModeAttribute(PlayerSimulationMode mode) : Attribute
{
    /// <inheritdoc cref="PlayerSimulationMode"/>
    public PlayerSimulationMode Mode { get; } = mode;
}