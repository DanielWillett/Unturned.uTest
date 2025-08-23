using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace uTest;

/// <summary>
/// Contains context about the current test being ran.
/// </summary>
public interface ITestContext
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
    string TestId { get; }

    /// <summary>
    /// The object used to run the tests.
    /// </summary>
    ITestClass Runner { get; }
    
    /// <summary>
    /// Token that gets cancelled when this test is cancelled.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Logs a message to the test output.
    /// </summary>
    void Log(LogSeverity logSeverity, string message);

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
    /// Configure the testing environment for this test.
    /// </summary>
    void Configure(Action<ITestConfigurationBuilder> configure);
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