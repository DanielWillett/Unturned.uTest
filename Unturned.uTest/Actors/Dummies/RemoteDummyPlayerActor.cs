using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Protocol;
using System;
using System.Diagnostics;

namespace uTest.Dummies;

/// <summary>
/// A dummy player which is ran as an instance of Unturned connecting over <c>localhost</c>.
/// Usually <see cref="SimulatedDummyPlayerActor"/> players can be used instead with much less overhead.
/// </summary>
public sealed class RemoteDummyPlayerActor : BaseServersidePlayerActor, IRpcSingleConnectionObject
{
    internal TaskCompletionSource<RemoteDummyPlayerActor>? _loadTcs;
    internal IModularRpcRemoteConnection? ConnectionIntl;

    /// <summary>
    /// The instance of the Unturned client process.
    /// </summary>
    public Process Process { get; internal set; }

    /// <summary>
    /// The PID of the instance of the Unturned client process.
    /// </summary>
    public uint ProcessId { get; internal set; }

    /// <summary>
    /// The ModularRPCs connection used to communicate with the client.
    /// </summary>
    public IModularRpcRemoteConnection Connection => ConnectionIntl ?? throw new InvalidOperationException("Not yet connected.");

    internal DummyReadyStatus Status { get; set; }

    public override bool IsRemotePlayer => true;

    /// <inheritdoc />
    internal RemoteDummyPlayerActor(CSteamID steam64, string displayName, DummyPlayerLauncher dummyLauncher, Process process)
        : base(steam64, displayName, dummyLauncher)
    {
        Process = process;
    }

}