using System;

namespace uTest.Module;

/// <summary>
/// Main accessor for the test host.
/// </summary>
public static class UnturnedTestHost
{
    private static IUnturnedTestRuntime? _runtime;

    /// <summary>
    /// The currently-running test runtime.
    /// </summary>
    /// <exception cref="InvalidOperationException">The runtime is not running.</exception>
    public static IUnturnedTestRuntime Runtime => _runtime ?? throw new InvalidOperationException("Runtime not running.");

    internal static void SetRuntime(IUnturnedTestRuntime? runtime)
    {
        _runtime = runtime;
    }
}

/// <summary>
/// The testing module.
/// </summary>
public interface IUnturnedTestRuntime
{
    /// <summary>
    /// The exception formatter to use when printing exceptions to your IDE.
    /// </summary>
    IExceptionFormatter? ExceptionFormatter { get; set; }

    /// <summary>
    /// The logger used to write to the console and log file.
    /// </summary>
    ILogger Logger { get; }
    
    /// <summary>
    /// Cancellation token which is cancelled if the IDE aborts a test run.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Cancels the current test run.
    /// </summary>
    void Cancel();
}