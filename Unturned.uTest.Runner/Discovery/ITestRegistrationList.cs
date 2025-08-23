using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace uTest.Runner;

internal interface ITestRegistrationList : ITestFrameworkCapability
{
    Task<List<UnturnedTest>> GetTestsAsync(CancellationToken token = default);
}
