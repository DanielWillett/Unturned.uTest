using System;
using uTest.Compat.Tests;

namespace uTest.Compat.Lifetime;

/// <summary>
/// Allows a framework to invoke code on test start and end.
/// </summary>
/// <remarks>Integrations can implement <see cref="IDisposable"/> to be disposed on shutdown.</remarks>
public interface ITestLifetimeIntegration
{
    /// <summary>
    /// Higher priority integrations will begin earlier and end later.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Invoked when a test is about to start.
    /// </summary>
    /// <param name="test">Information about the test being ran.</param>
    /// <param name="token">Cancellation token that will be invoked if the test run is cancelled.</param>
    /// <returns>
    /// A task that completes when the test is ready to proceed.
    /// If this method finishes with a result of <see langword="false"/>, the test will be skipped (<see cref="TestResult.Skipped"/>).
    /// </returns>
    Task<bool> BeginTestAsync(IUnitTestExecution test, CancellationToken token = default);

    /// <summary>
    /// Invoked after a test ends, no matter whether or not it was successful.
    /// </summary>
    /// <param name="test">Information about the test that was ran.</param>
    /// <param name="result">The result of the test. If this is <see langword="null"/>, it means there was an error somewhere before the test started running.</param>
    /// <param name="token">Cancellation token that will be invoked if the test run is cancelled.</param>
    /// <returns>A task that completes when the integration is done cleaning up after the test.</returns>
    Task EndTestAsync(IUnitTestExecution test, TestResult? result, CancellationToken token = default);
}