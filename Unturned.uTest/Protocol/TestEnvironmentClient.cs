using System;
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

    protected override void StartReconnecting()
    {
        _connectionTask = Task.Factory.StartNew(async () =>
        {
            await Semaphore.WaitAsync(TokenSource.Token);
            try
            {
                NamedPipeClientStream? old = Interlocked.Exchange(
                    ref PipeStream,
                    CreateNewStream()
                );

                if (old != null)
                {
                    try
                    {
                        old.Dispose();
                    }
                    catch { /* ignored */ }
                }

                try
                {
                    await PipeStream!.ConnectAsync(TokenSource.Token);

                    OnConnected();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            finally
            {
                Semaphore.Release();
            }

        }, TaskCreationOptions.LongRunning);
    }
}