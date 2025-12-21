namespace uTest;

/// <summary>
/// Used to configure the test environment prior to a test.
/// </summary>
public interface ITestConfigurationBuilder
{
    /// <summary>
    /// The environment being configured.
    /// </summary>
    IConfigurableTestEnvironment TestEnvironment { get; }

    /// <summary>
    /// The context of the test being ran.
    /// </summary>
    IUnconfiguredTestContext TestContext { get; }
}