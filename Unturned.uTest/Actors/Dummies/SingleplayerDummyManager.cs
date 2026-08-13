using SDG.Framework.Utilities;
using System;
using System.IO;
using uTest.Module;

namespace uTest.Dummies;

internal class SingleplayerDummyManager : IDummyPlayerController, IDisposable
{
    private readonly MainModule _module;
    private UnityCompletionSource? _connectCompletionSource;
    private UnityCompletionSource? _disconnectCompletionSource;

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

        _module.Logger.LogInformation($"Loading singleplayer: {State}.");
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

        _connectCompletionSource?.Dispose();
        _connectCompletionSource = UnityCompletionSource.Create(continueInstantly: false);
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

        if ((_events & 16) == 0)
        {
            Provider.onServerShutdown += OnServerShutdown;
            _events |= 16;
        }

        string map = config.Map ?? Player.Test!.Instance.Test.Map ?? "PEI";
        if (Level.getLevel(map) == null)
        {
            throw new InvalidOperationException($"Map {map} not found.");
        }

        TryDeleteWorld();

        _module.Logger.LogDebug($"Loading level in singleplayer: {map}.");
        Provider.map = map;
        Provider.singleplayer(config.Difficulty, config.HasCheats);

        // this may cause issues in the future but should be good for now and avoids extra patching
        // a couple things load from ServerSavedata in singleplayer(,) but don't seem to be important
        Dedicator.serverID = _module.TestList?.ServerId ?? "uTest";

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

        _disconnectCompletionSource?.Dispose();
        _disconnectCompletionSource = UnityCompletionSource.Create(continueInstantly: false);
        State = DummyState.Unloading;

        if (Level.isLoaded)
        {
            Provider.RequestDisconnect("Ending singleplayer test.");
        }
        else
        {
            _module.Logger.LogWarning("Level not loaded while despawning actors.");
        }

        if ((_events & 8) == 0)
        {
            Level.onLevelLoaded += OnOtherLevelLoaded;
            _events |= 8;
        }

        await _disconnectCompletionSource.Task;

        await GameThread.Switch();

        // should've already happened
        if (Player.IsOnline)
            Player.NotifyDisconnected();

        if ((_events & 16) != 0)
        {
            Provider.onServerShutdown -= OnServerShutdown;
            _events &= ~16;
        }

        TryDeleteWorld();

        _module.Logger.LogInformation("Ended singleplayer test.");
    }

    private void OnServerShutdown()
    {
        Player.NotifyDisconnected();
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

    private void OnOtherLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_MENU)
            return;

        if ((_events & 8) != 0)
        {
            Level.onLevelLoaded -= OnOtherLevelLoaded;
            _events &= ~8;
        }

        State = DummyState.Menu;
        _disconnectCompletionSource?.TryComplete();
        _disconnectCompletionSource = null;
    }

    private void OnUpdate()
    {
        if (State == DummyState.Loading)
        {
            if (!Player.IsOnline || SDG.Unturned.Player.isLoading || Level.isLoading)
                return;

            State = DummyState.Spawned;
            _module.Logger.LogDebug("Level finished loading.");
            _connectCompletionSource?.TryComplete();
            _connectCompletionSource = null;
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

        if ((_events & 8) != 0)
        {
            Level.onLevelLoaded -= OnOtherLevelLoaded;
        }

        if ((_events & 16) != 0)
        {
            Provider.onServerShutdown -= OnServerShutdown;
        }

        _events = 0;

        _connectCompletionSource?.TryCancel();
        _connectCompletionSource?.Dispose();
        _connectCompletionSource = null;

        _disconnectCompletionSource?.TryCancel();
        _disconnectCompletionSource?.Dispose();
        _disconnectCompletionSource = null;
    }

    public enum DummyState
    {
        Menu,

        Loading,

        Spawned,

        Unloading
    }
}