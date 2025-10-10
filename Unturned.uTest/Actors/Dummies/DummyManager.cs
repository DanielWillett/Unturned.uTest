using System;
using DanielWillett.ReflectionTools;
using uTest.Discovery;

namespace uTest.Dummies;

// 'Dummy' players are inspired by DiFFoZ's Dummy plugin: https://github.com/EvolutionPlugins/Dummy 
internal class DummyManager
{
    private readonly Dictionary<ulong, DummyPlayerActor> _dummies = new Dictionary<ulong, DummyPlayerActor>();

    public async Task StartDummiesForTestsAsync(UnturnedTestInstance[] tests)
    {
        bool needsDummies = false;
        bool needsFullPlayers = false;
        for (int i = 0; i < tests.Length; ++i)
        {
            ref UnturnedTestInstance test = ref tests[i];


        }
    }

    public bool TryGetDummy(Player player, [MaybeNullWhen(false)] out DummyPlayerActor dummy)
    {
        return _dummies.TryGetValue(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    internal void ListenOnServer()
    {
        foreach (DummyPlayerActor dummy in _dummies.Values)
        {
            while (dummy.ClientTransportIntl.TryDequeueOutgoingMessage(Provider.buffer, out long size))
            {
                NetMessages.ReceiveMessageFromClient(dummy.ClientTransportIntl.ServerSideConnection, Provider.buffer, 0, (int)size);
            }
        }
    }

    internal void ListenOnDummies()
    {
        foreach (DummyPlayerActor dummy in _dummies.Values)
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

    public static void SendMessageToServer(EServerMessage index, ENetReliability reliability, NetMessages.ClientWriteHandler callback, DummyPlayerActor actor)
    {

    }

    public DummyPlayerActor EndDummyConnect(IAsyncResult result)
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

internal class DummyConnectionState : IAsyncResult
{
    private ManualResetEvent? _resetEvent;
    private bool _disposed;

    internal ConnectionStage Stage;
    internal Exception? Exception;
    internal DummyPlayerActor? Actor;

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