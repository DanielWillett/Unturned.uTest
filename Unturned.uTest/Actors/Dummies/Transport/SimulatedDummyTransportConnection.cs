using SDG.NetPak;
using System;
using System.Globalization;
using System.Net;
using Unturned.SystemEx;

namespace uTest.Dummies;

// credit to DiFFoZ for this implementation
// https://github.com/EvolutionPlugins/Dummy/blob/openmod/Dummy/NetTransports/DummyTransportConnection.cs

internal class SimulatedDummyTransportConnection : ITransportConnection
{
    private readonly ushort _connectionPortSim;
    private readonly bool _hideIpAddress;
    private readonly uint _connectionIpSim;
    private readonly ulong _steamId;
    private bool _isClosed;

    public SimulatedDummyTransportConnection(ulong steam64, IPv4Address ip, ushort port, bool hideIpAddress)
    {
        _steamId = steam64;
        _connectionPortSim = port;
        _hideIpAddress = hideIpAddress;
        _connectionIpSim = ip.value;
    }

    public void Send(byte[] buffer, long size, ENetReliability reliability)
    {
        if (_isClosed)
            return;

        NetPakReader reader = NetMessages.GetInvokableReader();
        reader.SetBufferSegment(buffer, (int)Math.Min(size, int.MaxValue));
        reader.Reset();

        if (!reader.ReadEnum(out EClientMessage messageType))
        {
            UnturnedLog.error($"Dummy client ({_steamId}) received invalid message index from server.");
            return;
        }

        if (messageType == EClientMessage.InvokeMethod)
        {
            return;
        }

        if (!reader.ReadBits(NetReflection.clientMethodsBitCount, out uint messageIndex) || messageIndex >= NetReflection.clientMethodsLength)
        {
            UnturnedLog.error($"Dummy client ({_steamId}) received invalid InvokeMethod index from server.");
            return;
        }

        ClientMethodInfo clientMethod = NetReflection.clientMethods[(int)messageIndex];
        if (!ShouldReadMessage(clientMethod))
        {
            return;
        }

        ClientInvocationContext ctx = new ClientInvocationContext(ClientInvocationContext.EOrigin.Remote, reader, clientMethod);
        clientMethod.readMethod(in ctx);
    }

    private static bool ShouldReadMessage(ClientMethodInfo method)
    {
        return method.declaringType == typeof(PlayerInput);
    }

    public bool TryGetIPv4Address(out uint address)
    {
        if (_hideIpAddress)
        {
            address = 0;
            return false;
        }

        address = _connectionIpSim;
        return true;
    }

    public bool TryGetPort(out ushort port)
    {
        if (_hideIpAddress)
        {
            port = 0;
            return false;
        }

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

    public string? GetAddressString(bool withPort)
    {
        if (_hideIpAddress)
        {
            return null;
        }

        string ip = Parser.getIPFromUInt32(_connectionIpSim);
        if (withPort)
            ip += $":{_connectionPortSim.ToString(CultureInfo.InvariantCulture)}";

        return ip;
    }

    public void CloseConnection()
    {
        _isClosed = true;
    }

    public bool Equals(ITransportConnection? other)
    {
        return this == (object?)other;
    }

    public override int GetHashCode()
    {
        return unchecked( (int)new CSteamID(_steamId).GetAccountID().m_AccountID );
    }

    public override string ToString()
    {
        return $"Simulated Dummy 0x{_steamId:D17}";
    }
}