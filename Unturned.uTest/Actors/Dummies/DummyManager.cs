using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.Configuration;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using DanielWillett.ReflectionTools;
using SDG.NetTransport.SteamNetworkingSockets;
using Unturned.SystemEx;
using uTest.Module;

namespace uTest.Dummies;

/// <summary>
/// Class responsible for dispatching calls between simulated or remote dummies, depending on the test.
/// <para> <br/>
/// Simulated dummies are ran in-process and can do simple things but aren't as realistic or flexable as remote bots. <br/>
/// Remote bots are literally an instance of the Unturned client that connects to the server and can do pretty much everything a player can.
/// </para>
/// </summary>
internal class DummyManager : IDisposable
{
    private readonly MainModule _module;

    internal readonly SteamIdPool SteamIdPool;

    internal RemoteDummyManager? RemoteDummies;
    internal SimulatedDummyManager? SimulatedDummies;

    internal bool HasServerDetails;
    internal ulong ServerDetailsCode;
    internal ushort ServerDetailsPort;
    internal IPv4Address ServerDetailsAddress;
    internal bool ServerHideClientIp;

    public DummyManager(MainModule module)
    {
        SteamIdPool = new SteamIdPool(module.TestList?.SteamIdGenerationStyle ?? SteamIdGenerationStyle.Random);
        _module = module;
    }


    public Task<bool> InitializeDummiesAsync(UnturnedTestInstanceData[] tests)
    {
        bool needsFullPlayers = false;
        bool needsSimulatedPlayers = false;
        int maxDummyCount = 0;
        int minFullDummies = 0;
        List<PlayerCountAttribute> playerCountListTemp = new List<PlayerCountAttribute>();
        List<PlayerSimulationModeAttribute> playerModeListTemp = new List<PlayerSimulationModeAttribute>();
        for (int i = 0; i < tests.Length; ++i)
        {
            UnturnedTestInstanceData test = tests[i];

            PlayerSimulationMode mode = PlayerSimulationMode.Dummy;
            TestAttributeHelper<PlayerSimulationModeAttribute>.GetAttributes(test.Instance.Method, playerModeListTemp);
            if (playerModeListTemp.Count > 0)
            {
                if (playerModeListTemp.Exists(x => x.Mode == PlayerSimulationMode.Full))
                    mode = PlayerSimulationMode.Full;

                playerModeListTemp.Clear();
            }

            test.SimulationMode = mode;

            if (mode == PlayerSimulationMode.Full)
                needsFullPlayers = true;
            else
                needsSimulatedPlayers = true;

            TestAttributeHelper<PlayerCountAttribute>.GetAttributes(test.Instance.Method, playerCountListTemp);
            if (playerCountListTemp.Count == 0)
                continue;

            PlayerCountAttribute max = playerCountListTemp.Aggregate((a, max) => a.PlayerCount > max.PlayerCount ? a : max);
            maxDummyCount = Math.Max(max.PlayerCount, maxDummyCount);
            if (mode == PlayerSimulationMode.Full)
            {
                minFullDummies = Math.Max(max.PlayerCount, minFullDummies);
            }

            test.Dummies = max.PlayerCount;
            playerCountListTemp.Clear();
        }

        if (maxDummyCount > byte.MaxValue)
        {
            _module.Logger.LogError($"At least one test requires more dummies than players supported by Unturned: {maxDummyCount}. There should not be more than {byte.MaxValue} dummies.");
            return Task.FromResult(false);
        }

        GameThread.Run(maxDummyCount, minDummies => Provider.maxPlayers = (byte)minDummies);

        if (maxDummyCount <= 0)
        {
            return Task.FromResult(true);
        }

        if (needsSimulatedPlayers)
        {
            SimulatedDummies ??= new SimulatedDummyManager(_module, _module.Logger);
        }

        if (!needsFullPlayers)
        {
            return Task.FromResult(true);
        }

        if (RemoteDummies == null)
        {
            ServiceContainer cont = new ServiceContainer();

            IReflectionToolsLogger logger = Accessor.Logger ?? DefaultLoggerReflectionTools.Logger;

            ProxyGenerator.Instance.SetLogger(logger);
            ServerRpcConnectionLifetime lifetime = new ServerRpcConnectionLifetime();
            DefaultSerializer serializer = new DefaultSerializer(new SerializationConfiguration
            {
                MaximumGlobalArraySize = 256,
                MaximumArraySizes = { { typeof(byte), 16384 } },
                MaximumStringLength = 16384
            });

            cont.AddService(typeof(ProxyGenerator), ProxyGenerator.Instance);
            cont.AddService(typeof(IRpcConnectionLifetime), lifetime);
            cont.AddService(typeof(IRpcSerializer), serializer);

            DependencyInjectionRpcRouter router = new DependencyInjectionRpcRouter(cont);
            lifetime.SetLogger(logger);
            router.SetLogger(logger);

            cont.AddService(typeof(IRpcRouter), router);

            RemoteDummies = ProxyGenerator.Instance.CreateProxy<RemoteDummyManager>(router, true, _module, _module.Logger, cont);
            cont.AddService(typeof(RemoteDummyManager), RemoteDummies);
            cont.AddService(typeof(IDummyPlayerController), RemoteDummies);
        }

        return RemoteDummies.StartDummiesAsync(minFullDummies);
    }

    internal Task InitializeDummiesForTestAsync(UnturnedTestInstanceData test, CancellationToken token = default)
    {
        // clear steam ID cache for ReadyToConnect message so it doesn't interfere with next test
        ServerMessageHandler_ReadyToConnect.joinRateLimiter.steamIdRateLimitingLog.Clear();

        ClearServerDetailsCache();
        return Task.CompletedTask;
    }

    internal IReadOnlyList<IServersideTestPlayer>? AllocateDummiesToTest(UnturnedTestInstanceData test, out bool overflow)
    {
        if (test.Dummies <= 0)
        {
            test.AllocatedDummies = null;
            overflow = false;
            return null;
        }

        IServersideTestPlayer[] players = new IServersideTestPlayer[test.Dummies];
        int ct = 0;
        if (test.SimulationMode == PlayerSimulationMode.Full)
        {
            if (RemoteDummies == null)
            {
                test.AllocatedDummies = null;
                overflow = true;
                return null;
            }

            foreach (RemoteDummyPlayerActor actor in RemoteDummies.Dummies.Values)
            {
                if (actor.Status is not (DummyReadyStatus.StartedUp or DummyReadyStatus.InMenu or DummyReadyStatus.Disconnecting)
                    || actor.Test != null)
                {
                    continue;
                }

                players[ct] = actor;
                actor.Test = test;
                actor.Index = ct;
                ++ct;
                if (ct >= test.Dummies)
                    break;
            }
        }
        else
        {
            if (SimulatedDummies == null)
            {
                test.AllocatedDummies = null;
                overflow = true;
                return null;
            }

            foreach (SimulatedDummyPlayerActor actor in SimulatedDummies.Dummies.Values)
            {
                if (actor.IsOnline || actor.Test != null)
                    continue;

                players[ct] = actor;
                actor.Test = test;
                actor.Index = ct;
                ++ct;
                if (ct >= test.Dummies)
                    break;
            }
        }

        if (ct < test.Dummies)
        {
            Array.Resize(ref players, ct);
            overflow = true;
        }
        else
        {
            overflow = false;
        }

        test.AllocatedDummies = players;
        return players;
    }

    internal void DeallocateDummies(UnturnedTestInstanceData test)
    {
        IServersideTestPlayer[]? oldDummies = Interlocked.Exchange(ref test.AllocatedDummies, null);
        if (oldDummies == null)
            return;

        foreach (IServersideTestPlayer player in oldDummies)
        {
            player.Test = null;
            player.Index = -1;
        }
    }

    internal void ClearServerDetailsCache()
    {
        HasServerDetails = false;
    }

    internal void EnsureServerDetailsCached()
    {
        if (HasServerDetails)
            return;

        ulong serverCode = SteamGameServer.GetSteamID().m_SteamID;
        ushort port = Provider.GetServerConnectionPort();

        if (!IPv4Address.TryParse(Provider.bindAddress, out IPv4Address address))
        {
            address = new IPv4Address((127u << 24) | 1 /* 127.0.0.1 */);
        }

        ServerDetailsCode = serverCode;
        ServerDetailsPort = port;
        ServerDetailsAddress = address;
        ServerHideClientIp = Provider.configData.Server.Use_FakeIP
                             || !ServerTransport_SteamNetworkingSockets.clUseIpSocket.value;
        HasServerDetails = true;
    }

    public bool TryGetDummy(Player player, [MaybeNullWhen(false)] out BaseServersidePlayerActor dummy)
    {
        return TryGetDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetDummy(ulong steam64, [MaybeNullWhen(false)] out BaseServersidePlayerActor dummy)
    {
        if (SimulatedDummies != null && SimulatedDummies.TryGetSimulatedDummy(steam64, out SimulatedDummyPlayerActor? simDummy))
        {
            dummy = simDummy;
            return true;
        }

        if (RemoteDummies != null && RemoteDummies.TryGetRemoteDummy(steam64, out RemoteDummyPlayerActor? remDummy))
        {
            dummy = remDummy;
            return true;
        }

        dummy = null;
        return false;
    }

    /// <summary>
    /// Deletes any player data belonging to dummy players.
    /// </summary>
    public void ClearPlayerDataFromDummies()
    {
        string rootDir = PlayerSavedata.hasSync
            ? $"{ReadWrite.PATH}/Sync"
            : $"{ReadWrite.PATH}{ServerSavedata.directory}/{Provider.serverID}/Players";

        if (!Directory.Exists(rootDir))
            return;

        foreach (string dir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsBotPlayerDir(dir))
                continue;

            string[] subDirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            if (subDirs.Length > 1)
            {
                for (int i = 0; i < subDirs.Length; ++i)
                {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                    if (!Path.GetFileName(subDirs[i].AsSpan()).Equals(Provider.map, FileHelper.FileNameComparison))
#else
                    if (!string.Equals(Path.GetFileName(subDirs[i]), Provider.map, FileHelper.FileNameComparison))
#endif
                    {
                        continue;
                    }

                    TryDeleteFolder(subDirs[i]);
                    break;
                }

                continue;
            }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            if (subDirs.Length == 1 && !Path.GetFileName(subDirs[0].AsSpan()).Equals(Provider.map, FileHelper.FileNameComparison))
#else
            if (subDirs.Length == 1 && !string.Equals(Path.GetFileName(subDirs[0]), Provider.map, FileHelper.FileNameComparison))
#endif
            {
                continue;
            }

            TryDeleteFolder(dir);
        }
    }

    private void TryDeleteFolder(string dir)
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            _module.Logger.LogError($"Error deleting bot player data directory \"{dir}\".", ex);
        }
    }

    private bool IsBotPlayerDir(string dir)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        ReadOnlySpan<char> fn = Path.GetFileName(dir.AsSpan());
#else
        string fn = Path.GetFileName(dir);
#endif
        // 292733980074184636_0
        int index = fn.IndexOf('_');
        if (index is < 17 or > 18)
            return false;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        if (!ulong.TryParse(fn.Slice(0, index), NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId))
#else
        if (!ulong.TryParse(fn.Substring(0, index), NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId))
#endif
            return false;


        return SteamIdPool.IsLikelyGeneratedId(new CSteamID(steamId));
    }

    public void Dispose()
    {
        RemoteDummies?.CloseAllDummies();
    }
}

internal class DummyConnectionState : IAsyncResult
{
    private ManualResetEvent? _resetEvent;
    private bool _disposed;

    internal ConnectionStage Stage;
    internal Exception? Exception;
    internal SimulatedDummyPlayerActor? Actor;

    public bool IsCompleted { [MemberNotNullWhen(true, nameof(Actor))] get; internal set; }

    public WaitHandle AsyncWaitHandle
    {
        get
        {
            if (_resetEvent != null)
                return _resetEvent;

            if (_disposed)
                throw new ObjectDisposedException(nameof(DummyConnectionState));

            ManualResetEvent temp;
            ManualResetEvent? old = Interlocked.CompareExchange(ref _resetEvent, temp = new ManualResetEvent(false), null);
            if (old != null)
            {
                temp.Dispose();
                return old;
            }

            if (!_disposed)
                return temp;

            Interlocked.CompareExchange(ref _resetEvent, null, temp);
            temp.Dispose();
            throw new ObjectDisposedException(nameof(DummyConnectionState));
        }
    }

    public object? AsyncState { get; }

    public DummyConnectionState(object? state)
    {
        AsyncState = state;
        IsCompleted = false;
        Stage = ConnectionStage.None;
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Exchange(ref _resetEvent, null)?.Dispose();
    }

    bool IAsyncResult.CompletedSynchronously => false;

    internal enum ConnectionStage
    {
        None,

        /// <summary>
        /// Client asks server for workshop files.
        /// </summary>
        /// <remarks><see cref="Provider.connect"/> (onClientTransportReady)</remarks>
        GetWorkshopFiles,

        /// <summary>
        /// Server sends client the workshop files and other various settings.
        /// </summary>
        /// <remarks><see cref="ServerMessageHandler_GetWorkshopFiles"/>.</remarks>
        DownloadWorkshopFiles,
    }
}