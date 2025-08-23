using System;
using System.IO.Pipes;

namespace uTest.Protocol;

/// <summary>
/// Hosts test results and allows for triggering tests for test adapters.
/// </summary>
internal class TestEnvironmentServer : IDisposable
{
    public const string PipeName = "Unturned.uTest.AdapterServer";

    private readonly NamedPipeServerStream _serverStream;

    private readonly CancellationTokenSource _cts;
    private readonly bool _isRunningInUnturned;

    public TestEnvironmentServer(bool isRunningInUnturned)
    {
        _isRunningInUnturned = isRunningInUnturned;
        _serverStream = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        _cts = new CancellationTokenSource();

        Task.Factory.StartNew(async () =>
        {
            await _serverStream.WaitForConnectionAsync(_cts.Token);

            OnConnected();

        }, TaskCreationOptions.LongRunning);
    }

    private void Info(string msg)
    {
        if (_isRunningInUnturned)
            CommandWindow.Log(msg);
        else
            Console.WriteLine(msg);
    }

    private void OnConnected()
    {
        Info("Client connected.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_cts.IsCancellationRequested)
        {
            try
            {
                _cts.Cancel();
            }
            catch { /* ignored */ }
        }
        try
        {
            _serverStream.Disconnect();
        }
        catch { /* ignored */ }
        _serverStream.Dispose();
    }
}
