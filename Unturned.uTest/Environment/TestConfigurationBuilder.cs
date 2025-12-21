using System;

namespace uTest.Environment;

internal class TestConfigurationBuilder(IUnconfiguredTestContext context) : ITestConfigurationBuilder
{
    public IUnconfiguredTestContext TestContext { get; } = context;
    public IConfigurableTestEnvironment TestEnvironment => throw new NotImplementedException();
}