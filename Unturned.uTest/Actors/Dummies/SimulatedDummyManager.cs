using System;
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
    public async Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token)
    {
        if (player is not SimulatedDummyPlayerActor actor)
            throw new ArgumentException("Expected SimulatedDummyPlayerActor.", nameof(actor));

        await GameThread.Switch(token);

        if (!actor.IsOnline)
        {
            if (ignoreAlreadyDisconnected)
                return;

            throw new InvalidOperationException("Player already disconnected.");
        }

        Provider.dismiss(player.Steam64);
        // actor.NotifyDisconnected called by DummyManager.onServerDisconnected
        
        if (player.IsOnline)
        {
            throw new TimeoutException("Timeout disconnecting player.");
        }

        _logger.LogTrace($"Removed simulated dummy {actor.DisplayName}.");
    }

    /// <inheritdoc />
    public async Task SpawnPlayerAsync(IServersideTestPlayer player, Action<DummyPlayerJoinConfiguration>? configurePlayers, bool ignoreAlreadyConnected, CancellationToken token)
    {
        if (player is not SimulatedDummyPlayerActor actor)
            throw new ArgumentException("Expected SimulatedDummyPlayerActor.", nameof(actor));

        await GameThread.Switch(token);

        if (actor.IsOnline)
        {
            if (ignoreAlreadyConnected)
                return;

            throw new InvalidOperationException("Player already connected.");
        }

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

        ulong[] packageSkins = actor.Configuration.GetEquippedSkinsArray(out string[] skinTags, out string[] skinDynamicProps);
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
            packageSkins,
            actor.Configuration.Skillset,
            actor.Configuration.Language,
            actor.Configuration.SteamLobbyId,
            actor.Configuration.Platform
        )
        {
            shirtItem = actor.Configuration.ShirtItem.m_SteamItemDef,
            pantsItem = actor.Configuration.PantsItem.m_SteamItemDef,
            hatItem = actor.Configuration.HatItem.m_SteamItemDef,
            backpackItem = actor.Configuration.BackpackItem.m_SteamItemDef,
            vestItem = actor.Configuration.VestItem.m_SteamItemDef,
            maskItem = actor.Configuration.MaskItem.m_SteamItemDef,
            glassesItem = actor.Configuration.GlassesItem.m_SteamItemDef,
            skinItems = DummyPlayerJoinConfiguration.ConvertEquippedSkinsArray(packageSkins),
            skinTags = skinTags,
            skinDynamicProps = skinDynamicProps,
            hasAuthentication = true,
            hasGroup = true,
            hasProof = true,
            assignedPro = actor.Configuration.HasGold,
            assignedAdmin = actor.Configuration.HasAdmin
        };

        int pendingPos = Provider.pending.Count;

        Provider._transportConnectionToPendingPlayerMap[connection] = pending;
        Provider.pending.Add(pending);

        bool isRemovedFromPending = false;
        bool couldBeAddedToClients = false;

        try
        {
            if (CheckShouldBeBanned(actor, pending, connectionAddress))
            {
                throw new ActorDestroyedException(
                    actor,
                    string.Format(Properties.Resources.ActorDestroyed_FailedToConnect, actor.DisplayName, nameof(ESteamConnectionFailureInfo.BANNED))
                );
            }

            if (CheckRejectedByPlugin(actor, pending))
            {
                throw new ActorDestroyedException(
                    actor,
                    string.Format(Properties.Resources.ActorDestroyed_FailedToConnect, actor.DisplayName, nameof(ESteamConnectionFailureInfo.PLUGIN))
                );
            }

            couldBeAddedToClients = true;
            Provider.accept(pending);
            if (Provider.clients.Count == 0 || (object)Provider.clients[^1].playerID != playerId)
            {
                throw new ActorDestroyedException(
                    actor,
                    string.Format(Properties.Resources.ActorDestroyed_FailedToConnect, actor.DisplayName, nameof(ESteamConnectionFailureInfo.NONE))
                );
            }

            isRemovedFromPending = true;
            _logger.LogTrace($"Added simulated dummy {actor.DisplayName}.");
            actor.NotifyConnected(Provider.clients[^1].player);
        }
        catch (Exception ex)
        {
            int index = couldBeAddedToClients ? Provider.clients.FindLastIndex(x => (object)x.playerID == playerId) : -1;
            if (index >= 0)
            {
                Provider.kick(playerId.steamID, ex.Message);
            }

            throw new ActorDestroyedException(
                actor,
                string.Format(Properties.Resources.ActorDestroyed_FailedToConnect, actor.DisplayName, nameof(ESteamConnectionFailureInfo.NONE)),
                ex
            );
        }
        finally
        {
            if (!isRemovedFromPending)
            {
                if (pendingPos < Provider.pending.Count && Provider.pending[pendingPos] == pending)
                {
                    Provider.broadcastRejectingPlayer(pending.playerID.steamID, ESteamRejection.PLUGIN, string.Empty);
                    Provider.pending.RemoveAt(pendingPos);
                }

                Provider._transportConnectionToPendingPlayerMap.Remove(connection);
            }
        }
    }

    private static bool CheckShouldBeBanned(SimulatedDummyPlayerActor actor, SteamPending pending, IPv4Address connectionAddress)
    {
        bool isBanned = false;
        string reason = string.Empty;
        uint duration = 0;
        if (SteamBlacklist.checkBanned(pending.playerID.steamID, connectionAddress.value, pending.playerID.GetHwids(), out SteamBlacklistID? blacklist))
        {
            isBanned = true;
            reason = blacklist.reason;
            duration = blacklist.getTime();
        }

        try
        {
            Provider.onCheckBanStatusWithHWID?.Invoke(pending.playerID, connectionAddress.value, ref isBanned, ref reason, ref duration);
        }
        catch (Exception ex)
        {
            // intentional non-use of ILogger to mimic vanilla
            UnturnedLog.warn("Plugin raised an exception from onCheckBanStatus:");
            UnturnedLog.exception(ex);
        }

        if (!isBanned)
            return false;

        actor.RejectReason = ESteamConnectionFailureInfo.BANNED;
        actor.RejectReasonString = string.IsNullOrEmpty(reason) ? null : reason;
        actor.RejectDuration = duration == SteamBlacklist.PERMANENT ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(duration);
        return true;
    }

    private static bool CheckRejectedByPlugin(SimulatedDummyPlayerActor actor, SteamPending pending)
    {
        if (Provider.onCheckValidWithExplanation == null)
            return false;

        bool isValid = true;
        string explanation = string.Empty;

        try
        {
            ValidateAuthTicketResponse_t args;
            args.m_OwnerSteamID = pending.playerID.steamID;
            args.m_SteamID = pending.playerID.steamID;
            args.m_eAuthSessionResponse = EAuthSessionResponse.k_EAuthSessionResponseOK;
            Provider.onCheckValidWithExplanation.Invoke(args, ref isValid, ref explanation);
        }
        catch (Exception ex)
        {
            // intentional non-use of ILogger to mimic vanilla
            UnturnedLog.warn("Plugin raised an exception from onCheckValidWithExplanation or onCheckValid:");
            UnturnedLog.exception(ex);
        }

        if (isValid)
            return false;

        actor.RejectReason = ESteamConnectionFailureInfo.PLUGIN;
        actor.RejectReasonString = explanation;
        return true;
    }
}
