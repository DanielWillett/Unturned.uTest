using SDG.Framework.Utilities;
using System;
using System.IO;
using uTest.Module;

namespace uTest.Dummies;

internal class SingleplayerDummyManager : IDummyPlayerController, IDisposable
{
    private readonly MainModule _module;
    private TaskCompletionSource<int>? _connectCompletionSource;
    private TaskCompletionSource<int>? _disconnectCompletionSource;

    private int _events;

    public SingleplayerServersideTestPlayer Player { get; }

    public DummyState State
    {
        get;
        private set
        {
#if DEBUG
            _module.Logger.LogInformation($"State changed: {field} -> {value}.");
#endif
            field = value;
        }
    }

    public SingleplayerDummyManager(MainModule module)
    {
        _module = module;
        Player = new SingleplayerServersideTestPlayer(this);

        State = DummyState.Menu;
    }

    public async Task SpawnPlayerAsync(
        IServersideTestPlayer player,
        Action<DummyPlayerJoinConfiguration>? configurePlayers,
        bool ignoreAlreadyConnected,
        CancellationToken token)
    {
        if ((object)player != Player)
        {
            // should be impossible
            throw new InvalidOperationException("This player doesn't belong to this controller.");
        }

        await GameThread.Switch(token);

        if (State is DummyState.Loading)
        {
            if (_connectCompletionSource == null)
                throw new Exception("Loading but doesn't have TCS.");

            await _connectCompletionSource.Task;
            await GameThread.Switch(token);
            // now State = Spawned
        }

        if (State == DummyState.Unloading)
        {
            throw new ActorDestroyedException(Player, "Player is still disconnecting.");
        }

        if (State == DummyState.Spawned)
        {
            if (ignoreAlreadyConnected)
                return;

            throw new InvalidOperationException("Player is already spawned.");
        }

        _connectCompletionSource = new TaskCompletionSource<int>();
        State = DummyState.Loading;
        
        SingleplayerJoinConfiguration config = new SingleplayerJoinConfiguration(Player.Steam64, Player.DisplayName);

        Player.Configure(config, configurePlayers);

        if ((_events & 1) == 0)
        {
            Level.onPostLevelLoaded += OnLevelLoaded;
            _events |= 1;
        }

        if ((_events & 4) == 0)
        {
            Provider.onEnemyConnected += OnEnemyConnected;
            _events |= 4;
        }

        string map = config.Map ?? Player.Test!.Instance.Test.Map ?? "PEI";
        if (Level.getLevel(map) == null)
        {
            throw new InvalidOperationException($"Map {map} not found.");
        }

        TryDeleteWorld();

        Provider.map = map;
        Provider.singleplayer(config.Difficulty, config.HasCheats);
        Dedicator.serverID = "uTest"; // this may cause issues in the future but should be good for now
        config.LevelConfigurer?.Invoke(Provider.configData, Provider.modeConfigData);
        await _connectCompletionSource.Task;
    }

    private void TryDeleteWorld()
    {
        string folder = Path.GetFullPath(ReadWrite.PATH + ServerSavedata.directory + "/uTest");
        try
        {
            Directory.Delete(folder, true);
        }
        catch (DirectoryNotFoundException) { }
        catch (Exception ex)
        {
            _module.Logger.LogError($"Failed to delete previous uTest World at {folder}.");
            _module.Logger.LogError(ex.ToString());
        }
    }

    public async Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token)
    {
        if ((object)player != Player)
        {
            // should be impossible
            throw new InvalidOperationException("This player doesn't belong to this controller.");
        }

        await GameThread.Switch(token);

        if (State is DummyState.Loading)
        {
            if (_connectCompletionSource == null)
                throw new Exception("Loading but doesn't have TCS.");

            await _connectCompletionSource.Task;
            await GameThread.Switch(token);
            // now State = Spawned
        }

        if (State == DummyState.Unloading)
        {
            if (_disconnectCompletionSource == null)
                throw new Exception("Unloading but doesn't have TCS.");

            await _disconnectCompletionSource.Task;
            await GameThread.Switch(token);
        }

        if (State == DummyState.Menu)
        {
            if (ignoreAlreadyDisconnected)
                return;

            throw new InvalidOperationException("Player is disconnected.");
        }

        _disconnectCompletionSource = new TaskCompletionSource<int>();
        State = DummyState.Unloading;

        Provider.RequestDisconnect("Ending singleplayer test.");

        if ((_events & 2) == 0)
        {
            TimeUtility.updated += OnUpdate;
            _events |= 2;
        }

        await _disconnectCompletionSource.Task;

        TryDeleteWorld();
    }

    private void OnEnemyConnected(SteamPlayer player)
    {
        Player.NotifyConnected(player.player);
        if ((_events & 4) == 0)
            return;

        Provider.onEnemyConnected -= OnEnemyConnected;
        _events &= ~4;
    }

    private void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;

        if ((_events & 1) != 0)
        {
            Level.onPostLevelLoaded -= OnLevelLoaded;
            _events &= ~1;
        }

        if ((_events & 2) != 0)
            return;

        TimeUtility.updated += OnUpdate;
        _events |= 2;
    }

    private void OnUpdate()
    {
        if (State == DummyState.Loading)
        {
            if (!Player.IsOnline || SDG.Unturned.Player.isLoading || Level.isLoading)
                return;

            State = DummyState.Spawned;
            _connectCompletionSource?.TrySetResult(0);
            _connectCompletionSource = null;
        }
        else if (State == DummyState.Unloading)
        {
            if (Level.isExiting)
                return;

            State = DummyState.Menu;
            _disconnectCompletionSource?.TrySetResult(0);
            _disconnectCompletionSource = null;
        }

        if ((_events & 2) == 0)
            return;

        TimeUtility.updated -= OnUpdate;
        _events &= ~2;
    }

    public void Dispose()
    {
        if ((_events & 1) != 0)
        {
            Level.onPostLevelLoaded -= OnLevelLoaded;
        }

        if ((_events & 2) != 0)
        {
            TimeUtility.updated -= OnUpdate;
        }

        if ((_events & 4) != 0)
        {
            Provider.onEnemyConnected -= OnEnemyConnected;
        }

        _connectCompletionSource?.TrySetCanceled();
        _connectCompletionSource = null;

        _disconnectCompletionSource?.TrySetCanceled();
        _disconnectCompletionSource = null;

        _events = 0;
    }

    public enum DummyState
    {
        Menu,

        Loading,

        Spawned,

        Unloading
    }
}