using OpenMod.Core.Eventing;
using uTest.Compat.Tests;

namespace uTest.Compat.OpenMod.Events;

/// <summary>
/// Invoked when a test completes.
/// </summary>
/// <remarks>
/// This will get emitted even if the test doesn't get completed successfully or doesn't pass.
/// If <see cref="TestBeginningEvent"/> was emitted, this event will be also.
/// </remarks>
public sealed class TestEndedEvent : Event
{
    /// <summary>
    /// The test that was ran.
    /// </summary>
    public required IUnitTestExecution Test { get; init; }

    /// <summary>
    /// If the test was ran successfully, the result of the test.
    /// </summary>
    /// <remarks>
    /// If this is <see langword="null"/>, it indicates that there was a problem somewhere before the test could even be ran.
    /// Check logs for more info.
    /// </remarks>
    public required TestResult? Result { get; init; }
}