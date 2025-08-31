using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace uTest.Runner;

internal interface ITestRegistrationList : ITestFrameworkCapability
{
    Task<List<UnturnedTest>> GetTestsAsync(Microsoft.Testing.Platform.Logging.ILogger logger, CancellationToken token = default);

    Task<List<UnturnedTestInstance>> ExpandTestsAsync(Microsoft.Testing.Platform.Logging.ILogger logger, List<UnturnedTest> originalTests, CancellationToken token = default);
}

internal readonly struct UnturnedTestInstance
{
    public UnturnedTest Test { get; }
    public UnturnedTestArgument[] Arguments { get; }

    public string Uid { get; }
    public string DisplayName { get; }

    public UnturnedTestInstance(UnturnedTest test, UnturnedTestArgument[] arguments, string uid, string displayName)
    {
        Test = test;
        Arguments = arguments;
        Uid = uid;
        DisplayName = displayName;
    }
}

internal readonly struct UnturnedTestArgument
{
    public object Value { get; }

}