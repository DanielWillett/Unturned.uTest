using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

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
    protected override void StartReconnecting()
    {
        Semaphore.Wait(TokenSource.Token);

        try
        {
            NamedPipeServerStream? old = Interlocked.Exchange(
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
        }
        catch
        {
            Semaphore.Release();
            throw;
        }

        NamedPipeServerStream? pipeStream = PipeStream;
        if (pipeStream == null)
        {
            Semaphore.Release();
            return;
        }

        ProbablyConnected = false;
        pipeStream.BeginWaitForConnection(OnConnectionReady, pipeStream);
    }

    private void OnConnectionReady(IAsyncResult ar)
    {
        NamedPipeServerStream pipeStream = (NamedPipeServerStream)ar.AsyncState;
        Semaphore.Release();

        try
        {
            pipeStream.EndWaitForConnection(ar);
            OnConnected();
        }
        catch (ObjectDisposedException)
        {
            ProbablyConnected = false;
        }
        catch (IOException)
        {
            ProbablyConnected = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error connecting to pipe stream.", ex);
        }
    }
}