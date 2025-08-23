namespace uTest;

/// <summary>
/// The result of a single test run.
/// </summary>
public enum TestResult
{
    /// <summary>
    /// A test was unable to determine a result.
    /// </summary>
    Inconclusive,

    /// <summary>
    /// The test passed.
    /// </summary>
    Pass,

    /// <summary>
    /// The test failed.
    /// </summary>
    Fail,

    /// <summary>
    /// The test run was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The test hit it's timeout.
    /// </summary>
    Timeout
}
