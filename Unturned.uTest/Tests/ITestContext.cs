using JetBrains.Annotations;
using System;
using System.Reflection;
using uTest.Dummies;

namespace uTest;

/// <summary>
/// Contains context about the current test being ran.
/// </summary>
/// <remarks>Base interface of <see cref="ITestContext"/> with reduced options available in <see cref="ITestContext.ConfigureAsync"/>.</remarks>
public interface IUnconfiguredTestContext
{
    /// <summary>
    /// The type that contains the tests being ran.
    /// </summary>
    Type TestClass { get; }
    
    /// <summary>
    /// The method being ran.
    /// </summary>
    MethodInfo TestMethod { get; }

    /// <summary>
    /// Unique ID referring to the test method being ran.
    /// </summary>
    UnturnedTestUid TestId { get; }

    /// <summary>
    /// The object used to run the tests.
    /// </summary>
    ITestClass Runner { get; }
    
    /// <summary>
    /// Token that gets cancelled when this test is cancelled.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// The logger used for this test.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Adds an artifact (basically an attachment) to this test.
    /// </summary>
    /// <param name="filePath">Path to a file.</param>
    /// <param name="displayName">The display name of the file. Defaults to the file's name.</param>
    /// <param name="description">A description of this file's meaning.</param>
    void AddArtifact(string filePath, string? displayName = null, string? description = null);
}

/// <summary>
/// Contains context about the current test being ran.
/// </summary>
public interface ITestContext : IUnconfiguredTestContext
{
    /// <summary>
    /// List of all dummies allocated to this test.
    /// </summary>
    IReadOnlyList<IServersideTestPlayer> Players { get; }

    /// <summary>
    /// Configure the testing environment for this test.
    /// </summary>
    /// <exception cref="InvalidOperationException">Test has already started.</exception>
    ValueTask ConfigureAsync([InstantHandle] Action<ITestConfigurationBuilder> configure);

    /// <summary>
    /// Simulates the console user typing into the server's terminal.
    /// </summary>
    /// <param name="command">The exact text to enter.</param>
    void SendTerminalInput(string command);

    /// <summary>
    /// Cancels this test run programmatically, including tests that have yet to run.
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
    [DoesNotReturn]
    void Cancel();

    /// <summary>
    /// Ignores this test programmatically, but continues to run other tests.
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
    [DoesNotReturn]
    void Ignore();

    /// <summary>
    /// Marks this test as inconclusive, ending the test immediately.
    /// </summary>
    /// <exception cref="TestResultException"/>
    [DoesNotReturn]
    void MarkInconclusive();

    /// <summary>
    /// Marks this test as a pass, ending the test immediately.
    /// </summary>
    /// <exception cref="TestResultException"/>
    [DoesNotReturn]
    void MarkPass();

    /// <summary>
    /// Marks this test as a failure, ending the test immediately.
    /// </summary>
    /// <exception cref="TestResultException"/>
    [DoesNotReturn]
    void MarkFailure();

    /// <summary>
    /// Notifies all allocated players that it's time to join the server and waits for them to fully spawn in and initialize.
    /// <para>
    /// To spawn individual players use <see cref="IServersideTestPlayer.SpawnAsync"/>.
    /// </para>
    /// </summary>
    /// <remarks>If no players are configured this method will do nothing.</remarks>
    /// <exception cref="TimeoutException">An actor did not connect in time.</exception>
    /// <exception cref="ActorDestroyedException">An actor disconnected/was rejected while connecting.</exception>
    ValueTask SpawnAllPlayersAsync(Action<DummyPlayerJoinConfiguration>? configurePlayers = null, CancellationToken token = default);

    /// <summary>
    /// Notifies all allocated players that it's time to disconnect from the server and waits for them to fully disconnect and return to the main menu.
    /// <para>
    /// To despawn individual players use <see cref="IServersideTestPlayer.DespawnAsync"/>.
    /// </para>
    /// </summary>
    /// <remarks>If no players are configured this method will do nothing.</remarks>
    /// <exception cref="AggregateException">Exceptions were thrown while disconnecting some players.</exception>
    ValueTask DespawnAllPlayersAsync(CancellationToken token = default);
}

public static class TestContext
{
    /// <summary>
    /// The context for the currently running test.
    /// </summary>
    [field: MaybeNull]
    public static ITestContext Current
    {
        get => field ?? throw new InvalidOperationException("Not in test.");
        internal set;
    }

    /// <summary>
    /// The logger for the currently running test. Identical to accessing <see cref="IUnconfiguredTestContext.Logger"/> from <see cref="Current"/>.
    /// </summary>
    public static ILogger Logger => Current.Logger;
}

/// <summary>
/// Defines the severity of log messages from tests.
/// </summary>
/// <remarks>The values of this enum intentionally line up with the <c>LogLevel</c> enum from Microsoft.Extensions.Logging.</remarks>
public enum LogSeverity
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}