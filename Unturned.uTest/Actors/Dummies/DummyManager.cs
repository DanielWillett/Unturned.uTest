using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.Configuration;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using uTest.Discovery;
using uTest.Module;

namespace uTest.Dummies;

// 'Dummy' players are inspired by DiFFoZ's Dummy plugin: https://github.com/EvolutionPlugins/Dummy 
internal class DummyManager : IDummyPlayerController
{
    private readonly MainModule _module;
    private readonly Dictionary<ulong, SimulatedDummyPlayerActor> _simulatedDummies = new Dictionary<ulong, SimulatedDummyPlayerActor>();

    private DummyPlayerLauncher? _remoteDummyLauncher;

    private PlayerSimulationMode _simMode = PlayerSimulationMode.Dummy;

    internal IDummyPlayerController? Controller => _remoteDummyLauncher;

    public DummyManager(MainModule module)
    {
        _module = module;
    }

    public Task<bool> InitializeDummiesAsync(UnturnedTestInstance[] tests)
    {
        bool needsFullPlayers = false;
        int minDummies = 0;
        List<PlayerCountAttribute> playerCountListTemp = new List<PlayerCountAttribute>();
        List<PlayerSimulationModeAttribute> playerModeListTemp = new List<PlayerSimulationModeAttribute>();
        for (int i = 0; i < tests.Length; ++i)
        {
            ref UnturnedTestInstance test = ref tests[i];

            if (!needsFullPlayers)
            {
                TestAttributeHelper<PlayerSimulationModeAttribute>.GetAttributes(test.Method, playerModeListTemp);
                if (playerModeListTemp.Count > 0)
                {
                    if (playerModeListTemp.Exists(x => x.Mode == PlayerSimulationMode.Full))
                        needsFullPlayers = true;

                    playerModeListTemp.Clear();
                }
            }

            TestAttributeHelper<PlayerCountAttribute>.GetAttributes(test.Method, playerCountListTemp);
            if (playerCountListTemp.Count == 0)
                continue;

            PlayerCountAttribute max = playerCountListTemp.Aggregate((a, max) => a.PlayerCount > max.PlayerCount ? a : max);
            minDummies = Math.Max(max.PlayerCount, minDummies);
            
            playerCountListTemp.Clear();
        }

        if (minDummies <= 0)
        {
            return Task.FromResult(true);
        }

        if (!needsFullPlayers)
            return Task.FromResult(true);

        _simMode = PlayerSimulationMode.Full;
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

        return _remoteDummyLauncher.StartDummiesAsync(minDummies);
    }

    internal ValueTask SpawnPlayersAsync(List<ulong>? idsOrNull, CancellationToken token)
    {
        if (_simMode == PlayerSimulationMode.Full)
        {
            return _remoteDummyLauncher!.ConnectDummyPlayersAsync(idsOrNull, token);
        }
        
        if (_simulatedDummies.Count > 0)
        {
            return new ValueTask(ConnectDummyPlayersAsync(idsOrNull, token));
        }

        return default;
    }

    private async Task ConnectDummyPlayersAsync(List<ulong>? idsOrNull, CancellationToken token)
    {
        List<SimulatedDummyPlayerActor> playersToConnect = new List<SimulatedDummyPlayerActor>();
        if (idsOrNull == null)
            playersToConnect.AddRange(_simulatedDummies.Values);
        else
        {
            foreach (ulong pl in idsOrNull)
            {
                if (!_simulatedDummies.TryGetValue(pl, out SimulatedDummyPlayerActor simPl))
                {
                    throw new ActorNotFoundException(pl.ToString("D17", CultureInfo.InvariantCulture));
                }

                playersToConnect.Add(simPl);
            }
        }

        // todo: BeginDummyConnect()
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