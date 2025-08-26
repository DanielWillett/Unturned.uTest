namespace uTest.Module;

/// <summary>
/// Possible exit codes for the Unturned process.
/// </summary>
public enum UnturnedTestExitCode
{
    /// <summary>
    /// All tests succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// At least one test failed or timed out.
    /// </summary>
    TestsFailed = 1,

    /// <summary>
    /// There was some other issue while starting up uTest or Unturned.
    /// </summary>
    StartupFailure = 2,
    
    /// <summary>
    /// Shutdown was requested by the uTest runner.
    /// </summary>
    GracefulShutdown = 3
}
