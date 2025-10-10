using System;
using Unturned.SystemEx;

namespace uTest.Dummies;

internal class DummyClientTransport : IClientTransport
{
    private readonly Queue<byte[]> _incomingMessageQueue;
    private readonly Queue<byte[]> _outgoingMessageQueue;

    public DummyTransportConnection ClientSideConnection { get; private set; }
    public DummyTransportConnection ServerSideConnection { get; private set; }

    public DummyClientTransport(DummyConnectionParameters parameters)
    {
        ClientSideConnection = new DummyTransportConnection(this, parameters, true);
        ServerSideConnection = new DummyTransportConnection(this, parameters, false);
        _incomingMessageQueue = new Queue<byte[]>();
        _outgoingMessageQueue = new Queue<byte[]>();
    }

    /// <inheritdoc />
    public void Initialize(ClientTransportReady callback, ClientTransportFailure failureCallback)
    {
        callback?.Invoke();
    }

    public void TearDown()
    {
        ServerSideConnection.CloseConnection();
        ClientSideConnection.CloseConnection();
    }

    internal void ReceiveMessageFromServer(byte[] message)
    {
        _incomingMessageQueue.Enqueue(message);
    }

    public bool Receive(byte[] buffer, out long size)
    {
        return TryDequeueMessageToBuffer(_incomingMessageQueue, buffer, out size);
    }

    internal bool TryDequeueOutgoingMessage(byte[] buffer, out long size)
    {
        return TryDequeueMessageToBuffer(_outgoingMessageQueue, buffer, out size);
    }

    private static bool TryDequeueMessageToBuffer(Queue<byte[]> messageQueue, byte[] buffer, out long size)
    {
        if (messageQueue.Count <= 0)
        {
            size = 0;
            return false;
        }

        byte[] msg = messageQueue.Dequeue();
        if (msg.Length > buffer.Length)
        {
            size = 0;
            return false;
        }

        size = msg.Length;
        Buffer.BlockCopy(msg, 0, buffer, 0, msg.Length);
        return true;
    }

    public void Send(byte[] buffer, long size, ENetReliability reliability)
    {
        byte[] message = new byte[size];
        Buffer.BlockCopy(buffer, 0, message, 0, (int)size);
        _outgoingMessageQueue.Enqueue(message);
        
        NetMessages.ReceiveMessageFromClient(ClientSideConnection, buffer, 0, (int)size);
    }

    public bool TryGetIPv4Address(out IPv4Address address)
    {
        if (ClientSideConnection.TryGetIPv4Address(out uint ip))
        {
            address = new IPv4Address(ip);
            return true;
        }

        address = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetConnectionPort(out ushort connectionPort)
    {
        return ClientSideConnection.TryGetPort(out connectionPort);
    }

    /// <inheritdoc />
    public bool TryGetQueryPort(out ushort queryPort)
    {
        if (!ClientSideConnection.TryGetPort(out ushort connectionPort))
        {
            queryPort = 0;
            return false;
        }

        queryPort = (ushort)((connectionPort + (ushort.MaxValue - 1)) % ushort.MaxValue);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetPing(out int pingMs)
    {
        pingMs = 0;
        return true;
    }
}
