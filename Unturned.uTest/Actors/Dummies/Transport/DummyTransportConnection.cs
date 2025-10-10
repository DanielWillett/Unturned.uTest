using System;
using System.Globalization;
using System.Net;

namespace uTest.Dummies;

internal class DummyTransportConnection : ITransportConnection
{
    private readonly DummyClientTransport _clientTransport;

    private readonly ushort _connectionPortSim;
    private readonly uint _connectionIpSim;
    private readonly ulong _steamId;
    private readonly bool _isClientSide;

    public DummyTransportConnection(DummyClientTransport clientTransport, DummyConnectionParameters parameters, bool isClientSide)
    {
        _clientTransport = clientTransport;
        _steamId = parameters.SteamId.m_SteamID;
        _connectionPortSim = parameters.ConnectionPort;
        _connectionIpSim = parameters.ConnectionIPv4.value;
        _isClientSide = isClientSide;
    }

    public bool Equals(ITransportConnection? other) => ReferenceEquals(other, this);

    public bool TryGetIPv4Address(out uint address)
    {
        address = _connectionIpSim;
        return true;
    }

    public bool TryGetPort(out ushort port)
    {
        port = _connectionPortSim;
        return true;
    }

    public bool TryGetSteamId(out ulong steamId)
    {
        steamId = _steamId;
        return true;
    }

    public IPAddress GetAddress()
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        Span<byte> span = stackalloc byte[4];
        span[0] = (byte)(_connectionIpSim >> 24);
        span[1] = (byte)(_connectionIpSim >> 16);
        span[2] = (byte)(_connectionIpSim >> 8);
        span[3] = (byte)_connectionIpSim;
        return new IPAddress(span);
#else
        return new IPAddress(new byte[] { (byte)(_connectionIpSim >> 24), (byte)(_connectionIpSim >> 16), (byte)(_connectionIpSim >> 8), (byte)_connectionIpSim });
#endif
    }

    public string GetAddressString(bool withPort)
    {
        string ip = Parser.getIPFromUInt32(_connectionIpSim);
        if (withPort)
            ip += $":{_connectionPortSim.ToString(CultureInfo.InvariantCulture)}";

        return ip;
    }

    public void CloseConnection()
    {

    }

    public void Send(byte[] buffer, long size, ENetReliability reliability)
    {
        if (_isClientSide)
        {
            _clientTransport.Send(buffer, size, reliability);
        }
        else
        {
            byte[] value = new byte[size];
            Buffer.BlockCopy(buffer, 0, value, 0, (int)size);
            _clientTransport.ReceiveMessageFromServer(value);
        }
    }
}
