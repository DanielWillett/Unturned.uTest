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
using uTest.Module;

namespace uTest.Dummies;

// 'Dummy' players are inspired by DiFFoZ's Dummy plugin: https://github.com/EvolutionPlugins/Dummy 
internal class DummyManager : IDummyPlayerController
{
    private readonly MainModule _module;
    private readonly Dictionary<ulong, SimulatedDummyPlayerActor> _simulatedDummies = new Dictionary<ulong, SimulatedDummyPlayerActor>();

    private DummyPlayerLauncher? _remoteDummyLauncher;

    internal SteamIdPool SteamIdPool { get; }

    internal IDummyPlayerController? Controller => _remoteDummyLauncher;

    public DummyManager(MainModule module)
    {
        SteamIdPool = new SteamIdPool(module.TestList?.SteamIdGenerationStyle ?? SteamIdGenerationStyle.Random);
        _module = module;
    }

    public Task<bool> InitializeDummiesAsync(UnturnedTestInstanceData[] tests)
    {
        bool needsFullPlayers = false;
        int minDummies = 0;
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

            TestAttributeHelper<PlayerCountAttribute>.GetAttributes(test.Instance.Method, playerCountListTemp);
            if (playerCountListTemp.Count == 0)
                continue;

            PlayerCountAttribute max = playerCountListTemp.Aggregate((a, max) => a.PlayerCount > max.PlayerCount ? a : max);
            minDummies = Math.Max(max.PlayerCount, minDummies);
            if (mode == PlayerSimulationMode.Full)
            {
                minFullDummies = Math.Max(max.PlayerCount, minFullDummies);
            }

            test.Dummies = max.PlayerCount;
            playerCountListTemp.Clear();
        }

        if (minDummies > byte.MaxValue)
        {
            _module.Logger.LogError($"At least one test requires more dummies than players supported by Unturned: {minDummies}. There should not be more than {byte.MaxValue} dummies.");
            return Task.FromResult(false);
        }

        GameThread.Run(minDummies, minDummies => Provider.maxPlayers = (byte)minDummies);

        if (minDummies <= 0)
        {
            return Task.FromResult(true);
        }

        if (!needsFullPlayers)
            return Task.FromResult(true);

        if (_remoteDummyLauncher == null)
        {
            ServiceContainer cont = new ServiceContainer();

            ProxyGenerator.Instance.SetLogger(DefaultLoggerReflectionTools.Logger);
            ServerRpcConnectionLifetime lifetime = new ServerRpcConnectionLifetime();
            DefaultSerializer serializer = new DefaultSerializer(new SerializationConfiguration
            {
                MaximumGlobalArraySize = 256,
                MaximumArraySizes = { { typeof(byte), 16384 }, { typeof(string), 16384 } }, // todo
                MaximumStringLength = 16384
            });

            cont.AddService(typeof(ProxyGenerator), ProxyGenerator.Instance);
            cont.AddService(typeof(IRpcConnectionLifetime), lifetime);
            cont.AddService(typeof(IRpcSerializer), serializer);

            DependencyInjectionRpcRouter router = new DependencyInjectionRpcRouter(cont);
            lifetime.SetLogger(DefaultLoggerReflectionTools.Logger);
            router.SetLogger(DefaultLoggerReflectionTools.Logger);

            cont.AddService(typeof(IRpcRouter), router);

            _remoteDummyLauncher = ProxyGenerator.Instance.CreateProxy<DummyPlayerLauncher>(router, true, _module, _module.Logger, cont);
            cont.AddService(typeof(DummyPlayerLauncher), _remoteDummyLauncher);
            cont.AddService(typeof(IDummyPlayerController), _remoteDummyLauncher);
        }

        return _remoteDummyLauncher.StartDummiesAsync(minFullDummies);
    }

    internal Task InitializeDummiesForTestAsync(UnturnedTestInstanceData test, CancellationToken token = default)
    {
        // clear steam ID cache for ReadyToConnect message so it doesn't interfere with next test
        ServerMessageHandler_ReadyToConnect.joinRateLimiter.steamIdRateLimitingLog.Clear();

        _remoteDummyLauncher?.ClearServerDetailsCache();
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
            if (_remoteDummyLauncher == null)
            {
                test.AllocatedDummies = null;
                overflow = true;
                return null;
            }

            foreach (RemoteDummyPlayerActor actor in _remoteDummyLauncher.RemoteDummies.Values)
            {
                if (actor.Status is not (DummyReadyStatus.StartedUp or DummyReadyStatus.InMenu or DummyReadyStatus.Disconnecting)
                    || actor.Test != null)
                {
                    continue;
                }

                players[ct] = actor;
                actor.Test = test;
                ++ct;
                if (ct >= test.Dummies)
                    break;
            }
        }
        else
        {
            foreach (SimulatedDummyPlayerActor actor in _simulatedDummies.Values)
            {
                if (actor.IsOnline || actor.Test != null)
                    continue;

                players[ct] = actor;
                actor.Test = test;
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
        }
    }

    /// <inheritdoc />
    public Task SpawnPlayerAsync(IServersideTestPlayer player, Action<DummyPlayerJoinConfiguration>? configurePlayers, bool ignoreAlreadyConnected, CancellationToken token)
    {
        // todo
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token)
    {
        // todo
        return Task.CompletedTask;
    }

    public bool TryGetDummy(Player player, [MaybeNullWhen(false)] out BaseServersidePlayerActor dummy)
    {
        return TryGetDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetDummy(ulong steam64, [MaybeNullWhen(false)] out BaseServersidePlayerActor dummy)
    {
        if (_simulatedDummies.TryGetValue(steam64, out SimulatedDummyPlayerActor simDummy))
        {
            dummy = simDummy;
            return true;
        }

        if (_remoteDummyLauncher != null && _remoteDummyLauncher.TryGetRemoteDummy(steam64, out RemoteDummyPlayerActor? remDummy))
        {
            dummy = remDummy;
            return true;
        }

        dummy = null;
        return false;
    }

    internal void ListenOnServer()
    {
        foreach (SimulatedDummyPlayerActor dummy in _simulatedDummies.Values)
        {
            while (dummy.ClientTransportIntl.TryDequeueOutgoingMessage(Provider.buffer, out long size))
            {
                NetMessages.ReceiveMessageFromClient(dummy.ClientTransportIntl.ServerSideConnection, Provider.buffer, 0, (int)size);
            }
        }
    }

    internal void ListenOnDummies()
    {
        foreach (SimulatedDummyPlayerActor dummy in _simulatedDummies.Values)
        {
            while (dummy.ClientTransportIntl.Receive(Provider.buffer, out long size))
            {
                NetMessages.ReceiveMessageFromServer(Provider.buffer, 0, (int)size);
            }
        }
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

    public void Dispose()
    {
        _remoteDummyLauncher?.CloseAllDummies();
    }

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