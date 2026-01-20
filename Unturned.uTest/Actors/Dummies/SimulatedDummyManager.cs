using System;
using System.Linq;
using SDG.NetTransport.SteamNetworkingSockets;
using UnityEngine.Assertions.Must;
using Unturned.SystemEx;
using uTest.Module;

namespace uTest.Dummies;

// Simulated 'Dummy' players are inspired by DiFFoZ's Dummy plugin: https://github.com/EvolutionPlugins/Dummy

/// <summary>
/// Simulated dummies are ran in-process but are a bit limited in what they can do compared to remote dummies.
/// Useful for when you just need a player for a test but they don't have to do anything special.
/// </summary>
internal class SimulatedDummyManager : IDummyPlayerController
{
    internal readonly Dictionary<ulong, SimulatedDummyPlayerActor> Dummies = new Dictionary<ulong, SimulatedDummyPlayerActor>();

    private readonly MainModule _module;
    private readonly ILogger _logger;

    public SimulatedDummyManager(MainModule module, ILogger logger)
    {
        _module = module;
        _logger = logger;
    }

    public bool TryGetSimulatedDummy(Player player, [MaybeNullWhen(false)] out SimulatedDummyPlayerActor dummy)
    {
        return TryGetSimulatedDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetSimulatedDummy(ulong steam64, [MaybeNullWhen(false)] out SimulatedDummyPlayerActor dummy)
    {
        return Dummies.TryGetValue(steam64, out dummy);
    }

    /// <inheritdoc />
    public async Task SpawnPlayerAsync(IServersideTestPlayer player, Action<DummyPlayerJoinConfiguration>? configurePlayers, bool ignoreAlreadyConnected, CancellationToken token)
    {
        if (player is not SimulatedDummyPlayerActor actor)
            throw new ArgumentException("Expected SimulatedDummyPlayerActor.", nameof(actor));

        await GameThread.Switch();
        actor.Configure(configurePlayers);

        SteamPlayerID playerId = new SteamPlayerID(
            actor.Steam64,
            actor.Configuration.CharacterIndex,
            actor.Configuration.PlayerName,
            actor.Configuration.CharacterName,
            actor.Configuration.NickName,
            actor.Configuration.SteamGroupId,
            actor.Configuration.ReportedHardwareIds
        );

        DummyManager dummyManager = _module.Dummies;
        dummyManager.EnsureServerDetailsCached();

        IPv4Address connectionAddress = actor.Configuration.ConnectionAddress;
        ushort port;
        if (actor.Configuration.ConnectionPort.HasValue)
        {
            port = actor.Configuration.ConnectionPort.Value;
        }
        else
        {
            ushort minPort = ReservedIPv4AddressHelper.MinDynamicPort;
            foreach (SimulatedDummyPlayerActor otherActor in Dummies.Values)
            {
                if (otherActor.RemoteAddress != connectionAddress)
                    continue;

                if (otherActor.RemotePort >= ReservedIPv4AddressHelper.MaxDynamicPort - 1)
                {
                    throw new InvalidOperationException(
                        $"Ran out of ports on address {otherActor.RemoteAddress.ToString()}. This shouldn't really happen since there can only be 255 online players."
                    );
                }

                minPort = (ushort)Math.Max(otherActor.RemotePort + 1, minPort);
            }

            port = minPort;
        }

        actor.RemotePort = port;
        actor.RemoteAddress = connectionAddress;

        ITransportConnection connection = new SimulatedDummyTransportConnection(
            actor.Steam64.m_SteamID,
            connectionAddress,
            port,
            dummyManager.ServerHideClientIp
        );

        SteamPending pending = new SteamPending(
            connection,
            playerId,
            actor.Configuration.HasGold,
            actor.Configuration.FaceIndex,
            actor.Configuration.HairIndex,
            actor.Configuration.BeardIndex,
            actor.Configuration.SkinColor,
            actor.Configuration.HairColor,
            actor.Configuration.MarkerColor,
            actor.Configuration.BeardColor,
            actor.Configuration.IsLeftHanded,
            unchecked ( (ulong)actor.Configuration.ShirtItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.PantsItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.HatItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.BackpackItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.VestItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.MaskItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.GlassesItem.m_SteamItemDef ),
            actor.Configuration.GetEquippedSkinsArray(out string[] skinTags, out string[] skinDynamicProps),
            actor.Configuration.Skillset,
            actor.Configuration.Language,
            actor.Configuration.SteamLobbyId,
            actor.Configuration.Platform
        );

        pending.shirtItem    = actor.Configuration.ShirtItem.m_SteamItemDef;
        pending.pantsItem    = actor.Configuration.PantsItem.m_SteamItemDef;
        pending.hatItem      = actor.Configuration.HatItem.m_SteamItemDef;
        pending.backpackItem = actor.Configuration.BackpackItem.m_SteamItemDef;
        pending.vestItem     = actor.Configuration.VestItem.m_SteamItemDef;
        pending.maskItem     = actor.Configuration.MaskItem.m_SteamItemDef;
        pending.glassesItem  = actor.Configuration.GlassesItem.m_SteamItemDef;

        pending.skinItems = DummyPlayerJoinConfiguration.ConvertEquippedSkinsArray(pending.packageSkins);
        pending.skinTags  = Array.Empty<string>();
    }

    /// <inheritdoc />
    public async Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token)
    {

    }

    public IAsyncResult BeginDummyConnect(DummyConnectionParameters parameters, AsyncCallback connectedCallback, object? state, CancellationToken token = default)
    {
        GameThread.Assert();

        DummyConnectionState result = new DummyConnectionState(state);

        return result;
    }

    public static void SendMessageToServer(EServerMessage index, ENetReliability reliability, NetMessages.ClientWriteHandler callback, SimulatedDummyPlayerActor actor)
    {

    }

    public SimulatedDummyPlayerActor EndDummyConnect(IAsyncResult result)
    {
        if (result is not DummyConnectionState state)
            throw new ArgumentException("IAsyncResult did not come from BeginDummyConnect.", nameof(result));

        if (!state.IsCompleted)
        {
            bool timedOut;
            try
            {
                timedOut = !state.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10d), true);
            }
            catch (ObjectDisposedException)
            {
                timedOut = false;
            }
            if (!state.IsCompleted)
            {
                state.Dispose();
                if (timedOut)
                    throw new TimeoutException("Timed out connecting player.", state.Exception);
                throw state.Exception ?? new Exception("Failed to connect player.");
            }
        }

        state.Dispose();

        return state.Actor;
    }
}
