namespace uTest;

/// <summary>
/// All uTest fixtures must implement this marker interface.
/// </summary>
public interface ITestClass;

/// <summary>
/// Allows uTest fixtures to implement a Setup method.
/// </summary>
public interface ITestClassSetup : ITestClass
{
    /// <summary>
    /// Allows a test class to define a setup procedure which is shared for all test methods in this class.
    /// </summary>
    ValueTask SetupAsync(ITestContext textContext, CancellationToken token);
}

/// <summary>
/// Allows uTest fixtures to implement a TearDown method.
/// </summary>
public interface ITestClassTearDown : ITestClass
{
    /// <summary>
    /// Allows a test class to define a tear-down procedure which is shared for all test methods in this class.
    /// </summary>
    /// <remarks>This method will be ran no matter if the test completes successfully or not.</remarks>
    ValueTask TearDownAsync(CancellationToken token);
}