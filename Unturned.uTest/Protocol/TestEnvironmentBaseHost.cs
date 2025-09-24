using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace uTest.Protocol;

/// <summary>
/// Hosts test results and allows for triggering tests for test adapters.
/// The Unturned instance is the server and the test runner is the client.
/// </summary>
public abstract class TestEnvironmentBaseHost<TPipeStream> : IDisposable where TPipeStream : PipeStream
{
    public const string PipeName = "Unturned.uTest.Runner";

    private readonly List<MessageHandler> _handlers;

    protected TPipeStream? PipeStream;
    protected readonly ILogger Logger;

    private CancellationTokenSource _cts;
    private readonly SemaphoreSlim _semaphore;
    private Task? _connectionTask;

    private bool _probablyConnected;

    private readonly byte[] _readBuffer;

    public bool IsServer { get; }

    public bool IsConnected => _probablyConnected;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<ITransportMessage>? MessageReceived;

    private protected TestEnvironmentBaseHost(bool isServer, ILogger logger, int bufferSize = 8192)
    {
        Logger = logger;
        IsServer = isServer;

        _readBuffer = new byte[bufferSize];

        _cts = new CancellationTokenSource();
        _semaphore = new SemaphoreSlim(1, 1);

        _probablyConnected = false;

        StartReconnecting();

        _handlers = new List<MessageHandler>(4);
    }

    public void DisconnectGracefully(bool startReconnecting = false)
    {
        CancellationTokenSource oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try
        {
            oldCts.Cancel();
        }
        finally
        {
            oldCts.Dispose();
        }

        ClosePipeStream();

        if (startReconnecting)
        {
            StartReconnecting();
        }
    }

    protected abstract TPipeStream CreateNewStream();
    protected abstract Task ConnectAsync(TPipeStream pipeStream, CancellationToken token = default);

    public async Task SendAsync(ITransportMessage message, CancellationToken token = default)
    {
        if (!_probablyConnected)
        {
            if (_connectionTask is { IsCompleted: false })
                await _connectionTask;
            if (!_probablyConnected)
            {
                throw new InvalidOperationException("Failed to connect.");
            }
        }

        await _semaphore.WaitAsync(token);
        try
        {
            if (PipeStream == null || !PipeStream.IsConnected)
            {
                _probablyConnected = false;
                throw new InvalidOperationException("Disconnected.");
            }

            await message.WriteAsync(PipeStream, token);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // WaitForPipeDrain only works on windows
                await Task.Factory.StartNew(() =>
                {
                    try
                    {
                        PipeStream.WaitForPipeDrain();
                    }
                    catch { /* ignored */ }
                }, TaskCreationOptions.LongRunning);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        if (!_probablyConnected)
        {
            throw new InvalidOperationException("Disconnected.");
        }
    }

    private void StartReconnecting()
    {
        _connectionTask = Task.Factory.StartNew(async () =>
        {
            await _semaphore.WaitAsync(_cts.Token);
            try
            {
                TPipeStream? old = Interlocked.Exchange(
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
                    await ConnectAsync(PipeStream!, _cts.Token);

                    OnConnected();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            finally
            {
                _semaphore.Release();
            }

        }, TaskCreationOptions.LongRunning);
    }

    private void OnConnected()
    {
        _probablyConnected = true;
        Logger.LogInformation("Connected to test environment host.");
        TPipeStream? stream = PipeStream;

        try
        {
            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error invoking Connected event.", ex);
        }

        if (stream != null)
        {
            stream.BeginRead(_readBuffer, 0, _readBuffer.Length, OnRead, stream);
        }
    }

    public IDisposable AddMessageHandler<TMessage>(Func<TMessage, bool> callback) where TMessage : ITransportMessage
    {
        MessageHandler<TMessage> messageHandler = new MessageHandler<TMessage>(callback, this);
        lock (_handlers)
        {
            _handlers.Add(messageHandler);
        }

        return messageHandler;
    }

    private void OnRead(IAsyncResult ar)
    {
        TPipeStream stream = (TPipeStream)ar.AsyncState;
        if (stream == null)
            return;

        int bytesRead = stream.EndRead(ar);
        if (bytesRead == 0)
        {
            _probablyConnected = false;
            Logger.LogInformation("Disconnected from test environment host.");
            try
            {
                Disconnected?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error invoking Disconnected event.", ex);
            }
            StartReconnecting();
            return;
        }

        try
        {
            int offset = 0;
            while (offset < bytesRead)
            {
                int messageMaxLength = bytesRead - offset;
                TestEnvironmentMessageType msg = (TestEnvironmentMessageType)_readBuffer[offset];
                ITransportMessage message;
                ArraySegment<byte> data;
                switch (msg)
                {
                    case TestEnvironmentMessageType.PredefinedMessage:
                        if (messageMaxLength < 2)
                        {
                            throw new FormatException("Malformed message, missing predefined message index.");
                        }

                        int index = _readBuffer[offset + 1];
                        if (index >= PredefinedMessages.Messages.Length)
                        {
                            throw new FormatException($"Malformed message, out of range predefined message index ({index}).");
                        }

                        message = PredefinedMessages.Messages[index]();
                        offset += 2;
                        data = new ArraySegment<byte>(_readBuffer, offset, messageMaxLength - 2);
                        break;

                    case TestEnvironmentMessageType.FullTypeName:
                        if (messageMaxLength < 3)
                        {
                            throw new FormatException("Malformed message, missing full type name length.");
                        }

                        int length = BitConverter.ToUInt16(_readBuffer, offset + 1);
                        if (length == 0 || messageMaxLength < length + 3)
                        {
                            throw new FormatException("Malformed message, missing full type name.");
                        }

                        string typeName = Encoding.UTF8.GetString(_readBuffer, offset + 3, length);
                        try
                        {
                            Type type = Type.GetType(typeName, throwOnError: true, ignoreCase: false)!;
                            if (!typeof(ITransportMessage).IsAssignableFrom(type))
                            {
                                throw new FormatException($"Malformed message, type is not ITransportMessage: \"{typeName}\".");
                            }

                            message = (ITransportMessage)Activator.CreateInstance(type);
                        }
                        catch (Exception ex)
                        {
                            throw new FormatException($"Malformed message, unknown full type name: \"{typeName}\".", ex);
                        }

                        length += 3;
                        offset += length;
                        data = new ArraySegment<byte>(_readBuffer, offset, messageMaxLength - length);
                        break;

                    default:
                        throw new FormatException($"Unknown message type: {msg}.");
                }

                try
                {
                    offset += message.Read(data);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Malformed message, failed to read message \"{message.GetType().FullName}\".", ex);
                }

                if (message is PingMessage { IsInitial: true } ping)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await SendAsync(new PingMessage(false, ping.TimeStamp));
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to respond to a ping.", ex);
                        }
                    });
                }
                else
                {
                    bool handled = false;
                    lock (_handlers)
                    {
                        foreach (MessageHandler handler in _handlers)
                        {
                            if (!handler.MessageType.IsInstanceOfType(message))
                                continue;

                            try
                            {
                                if (!handler.Handle(message))
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error invoking message handler: {handler}.", ex);
                            }

                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        try
                        {
                            MessageReceived?.Invoke(message);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error invoking MessageReceived event.", ex);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(null, ex);
        }

        if (!_cts.IsCancellationRequested && PipeStream == stream)
        {
            stream.BeginRead(_readBuffer, 0, _readBuffer.Length, OnRead, stream);
        }
    }

    protected virtual void Dispose(bool isDisposing)
    {
        _semaphore.Wait();
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel();
                }
                catch { /* ignored */ }
            }

            ClosePipeStream();
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }

    private void ClosePipeStream()
    {
        _probablyConnected = false;
        TPipeStream? stream = Interlocked.Exchange(ref PipeStream, null);
        if (stream == null)
            return;

        try
        {
            if (stream is NamedPipeServerStream svr)
                svr.Disconnect();
        }
        catch { /* ignored */ }
        finally
        {
            stream.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private abstract class MessageHandler : IDisposable
    {
        private readonly TestEnvironmentBaseHost<TPipeStream> _host;

        internal readonly Type MessageType;

        protected MessageHandler(Type messageType, TestEnvironmentBaseHost<TPipeStream> host)
        {
            MessageType = messageType;
            _host = host;
        }

        public abstract bool Handle(ITransportMessage message);

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_host._handlers)
            {
                _host._handlers.Remove(this);
            }
        }
    }

    private class MessageHandler<TMessage> : MessageHandler where TMessage : ITransportMessage
    {
        private readonly Func<TMessage, bool> _messageHandler;
        public MessageHandler(Func<TMessage, bool> messageHandler, TestEnvironmentBaseHost<TPipeStream> host)
            : base(typeof(TMessage), host)
        {
            _messageHandler = messageHandler;
        }

        /// <inheritdoc />
        public override bool Handle(ITransportMessage message)
        {
            return _messageHandler((TMessage)message);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _messageHandler.Method.ToString();
        }
    }
}

internal enum TestEnvironmentMessageType : byte
{
    /// <summary>
    /// Message is in <see cref="PredefinedMessages.Messages"/>.
    /// </summary>
    /// <remarks>Second byte is the index.</remarks>
    PredefinedMessage = 1,

    /// <summary>
    /// Message type is expressed using a UTF-8 string which is the assembly-qualified name of the type.
    /// </summary>
    /// <remarks>Length in UTF-8 bytes is expressed as a UInt16 little-endian number (second and third byte) before the raw UTF-8 data.</remarks>
    FullTypeName = 2
}

file static class PredefinedMessages
{
    public static readonly Func<ITransportMessage>[] Messages =
    [
        () => new PingMessage(),
        () => new LevelLoadedMessage(),
        () => new RefreshTestsMessage(),
        () => new RunTestsMessage(),
        () => new ReportTestResultMessage(),
        () => new GracefulShutdownMessage()
    ];
}