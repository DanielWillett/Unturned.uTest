#pragma warning disable IDE0130

namespace uTest;

#pragma warning restore IDE0130

/// <summary>
/// Describes how players are simulated. If any test in a run requires full simulation, all tests will use full simulation.
/// </summary>
#if !SOURCE_GEN
[JetBrains.Annotations.UsedImplicitly]
#endif
public enum PlayerSimulationMode
{
    /// <summary>
    /// Partially simulates a new player by creating a dummy managed by the server. May not perfectly mimic normal players in some cases, but is much less resource-intensive.
    /// </summary>
    Simulated,

    /// <summary>
    /// Fully simulates a new player by starting up an instance of the Unturned client and connecting it to the server for each player.
    /// </summary>
    Remote,

    /// <summary>
    /// Simulates one player in singleplayer mode. Overrides the player count to 1.
    /// </summary>
    Singleplayer
}