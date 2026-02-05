using OpenMod.API.Eventing;
using OpenMod.Core.Eventing;
using uTest.Compat.Tests;

namespace uTest.Compat.OpenMod.Events;

/// <summary>
/// Invoked when a test is being started.
/// </summary>
public sealed class TestBeginningEvent : Event, ICancellableEvent
{
    /// <summary>
    /// Whether or not this event has been cancelled. If the <see cref="TestBeginningEvent"/> is cancelled, the associated test will be skipped.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// The test that's about to get ran.
    /// </summary>
    public required IUnitTestExecution Test { get; init; }
}