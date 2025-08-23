using Microsoft.Testing.Platform.Extensions.TestHost;
using System.Reflection;

namespace uTest.Runner;

public class UnturnedTestExtension : ITestHostExtension
{
    private string? _version;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public string Uid => "Unturned.uTest";

    /// <inheritdoc />
    public string Version => _version ??= Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <inheritdoc />
    public string DisplayName => "uTest";

    /// <inheritdoc />
    public string Description => "Unit tests for Unturned.";
}
