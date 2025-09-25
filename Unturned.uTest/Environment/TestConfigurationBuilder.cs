using System;

namespace uTest.Environment;

internal class TestConfigurationBuilder(ITestContext context) : ITestConfigurationBuilder
{
    public ITestContext TestContext { get; } = context;
    public IConfigurableTestEnvironment TestEnvironment => throw new NotImplementedException();
}