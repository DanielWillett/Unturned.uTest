using System.IO.Pipes;
using System.Security.Principal;

namespace uTest.Protocol;

/// <summary>
/// The test host client running on the test runner.
/// </summary>
public class TestEnvironmentClient : TestEnvironmentBaseHost<NamedPipeClientStream>
{
    /// <inheritdoc />
    public TestEnvironmentClient(ILogger logger, int bufferSize = 8192) : base(false, logger, bufferSize) { }

    /// <inheritdoc />
    protected override NamedPipeClientStream CreateNewStream()
    {
        return new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.None
        );
    }

    /// <inheritdoc />
    protected override Task ConnectAsync(NamedPipeClientStream pipeStream, CancellationToken token = default)
    {
        return pipeStream.ConnectAsync(token);
    }
}