using System.IO.Pipes;

namespace uTest.Protocol;

/// <summary>
/// The test host server running on the Unturned instance.
/// </summary>
internal class TestEnvironmentServer : TestEnvironmentBaseHost<NamedPipeServerStream>
{
    /// <inheritdoc />
    internal TestEnvironmentServer(ILogger logger, int bufferSize = 8192) : base(true, logger, bufferSize) { }

    /// <inheritdoc />
    protected override NamedPipeServerStream CreateNewStream()
    {
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );
    }

    /// <inheritdoc />
    protected override Task ConnectAsync(NamedPipeServerStream pipeStream, CancellationToken token = default)
    {
        return pipeStream.WaitForConnectionAsync(token);
    }
}