using System;

namespace uTest;

/// <summary>
/// Defines the number of simulated players that need to be available for this test.
/// The players will not be online at the start of the test and will have to be explicitly connected.
/// <para>
/// Use the <see cref="PlayerSimulationModeAttribute"/> to specify which kind of simulated players need to be used.
/// </para>
/// <para>
/// When using <see cref="PlayerSimulationMode.Singleplayer"/>, this attribute is unnecessary and will default to one player. 
/// </para>
/// </summary>
/// <param name="playerCount">The number of players that need to be online for this test.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class PlayerCountAttribute(int playerCount) : Attribute
{
    internal bool HasSpawnAllPlayers;
    internal bool SpawnAllPlayersValue;

    /// <summary>
    /// The number of players that need to be online for this test.
    /// </summary>
    public int PlayerCount { get; } = playerCount;

    /// <summary>
    /// If <see langword="true"/>, will spawn all players with default settings before starting the test.
    /// </summary>
    /// <remarks>Otherwise, you will have to call <see cref="ITestContext.SpawnAllPlayersAsync"/> during the test.</remarks>
    public bool SpawnAllPlayers
    {
        get => SpawnAllPlayersValue;
        set
        {
            SpawnAllPlayersValue = value;
            HasSpawnAllPlayers = true;
        }

    }
}