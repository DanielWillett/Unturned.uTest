using System;
using System.Linq;

namespace uTest.Environment;

/// <summary>
/// The environment in place during a test. Disposal should revert all changes.
/// </summary>
public interface ITestEnvironment : IDisposable
{
    /// <summary>
    /// Service provider for tests.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// List of all players in this test.
    /// </summary>
    IReadOnlyList<ITestPlayer> Players { get; }
    
    /// <summary>
    /// Set of all barricades in the world.
    /// </summary>
    IQueryable<BarricadeActor> Barricades { get; }

    /// <summary>
    /// Set of all structures in the world.
    /// </summary>
    IQueryable<StructureActor> Structures { get; }

    /// <summary>
    /// The current in-game time. Date information should be ignored.
    /// </summary>
    DateTime Time { get; }

    /// <summary>
    /// The current in-game moon phase.
    /// </summary>
    MoonPhase MoonPhase { get; }
}