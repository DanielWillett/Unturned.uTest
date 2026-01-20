using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Unturned.SystemEx;
using uTest.Module;
using uTest.Patches;
using uTest.Protocol.DummyPlayerHost;
// ReSharper disable LocalizableElement

namespace uTest.Dummies;

/// <summary>
/// Handles launching fully simulated dummy clients.
/// </summary>
[GenerateRpcSource]
internal partial class RemoteDummyManager : IDummyPlayerController
{
    private readonly MainModule _module;
    private readonly ILogger _logger;
    private NamedPipeEndpoint? _rpcServer;

    internal readonly ConcurrentDictionary<ulong, RemoteDummyPlayerActor> Dummies = new ConcurrentDictionary<ulong, RemoteDummyPlayerActor>();

    internal IServiceProvider ModularRpcsServices { get; private set; }

    public RemoteDummyManager(MainModule module, ILogger logger, IServiceProvider serviceProvider)
    {
        _module = module;
        _logger = logger;
        ModularRpcsServices = serviceProvider;

        UnturnedTestPatches? p = _module.Patches;
        if (p != null)
        {
            p.RegisterPatch(SkipSteamAuthenticationForDummyPlayers.TryPatch, SkipSteamAuthenticationForDummyPlayers.TryUnpatch);
            p.RegisterPatch(RemoveWorkshopRateLimiter.TryPatch, RemoveWorkshopRateLimiter.TryUnpatch, critical: true);
            p.RegisterPatch(RemoveReadyToConnectChecks.TryPatch, RemoveReadyToConnectChecks.TryUnpatch, critical: true);
            p.RegisterPatch(RemoveAuthenticateChecks.TryPatch, RemoveAuthenticateChecks.TryUnpatch);
            p.RegisterPatch(IgnoreSocketExceptionsOnServer.TryPatch, IgnoreSocketExceptionsOnServer.TryUnpatch);
            p.RegisterPatch(WorkshopItemsQueriedUpdateDummies.TryPatch, WorkshopItemsQueriedUpdateDummies.TryUnpatch);
        }

        // pre-load this assembly.
        // Unturned's assembly resolution system isn't threadsafe so it corrupts the dictionary later on when loaded by ModularRPCs
        _ = Type.GetType("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost");
    }

    public bool TryGetRemoteDummy(Player player, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return TryGetRemoteDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetRemoteDummy(ulong steam64, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return Dummies.TryGetValue(steam64, out dummy);
    }

    private static string GetArgs(CSteamID steamId, string moduleDir, string dataDir)
    {
        return  "-NoDefaultLog " +
                "-fullscreenmode Windowed " +
                "-width 1280 " +
                "-height 720 " +
                //"-batchmode -nographics " +
               $"-ModulesPath {CommandLineHelper.EscapeCommandLineArg(moduleDir)} " +
               $"-uTestSteamId {steamId.m_SteamID.ToString(CultureInfo.InvariantCulture)} " +
               $"-uTestDataDir {CommandLineHelper.EscapeCommandLineArg(dataDir)} " +
                "-NoVanillaAssemblySearch " +
                "-NoWorkshopSubscriptions " +
                "-logfile -"; // '-logfile -' redirects output to stdout
    }

    public void NotifyWorkshopUpdate(PublishedFileId_t[] files)
    {
        ulong[] newArr = new ulong[files.Length];
        for (int i = 0; i < files.Length; ++i)
            newArr[i] = files[i].m_PublishedFileId;

        Task.Run(async () =>
        {
            try
            {
                await SendWorkshopItemsUpdate(Dummies.Values
                    .Select(x => x.ConnectionIntl)
                    .Where(x => x != null)!, newArr);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending new workshop items to client.", ex);
            }
        });
    }

    [RpcSend("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost", "ReceiveWorkshopItemsUpdate")]
    private partial RpcTask SendWorkshopItemsUpdate(IEnumerable<IModularRpcRemoteConnection> connections, ulong[] files);

    [RpcReceive]
    private void ReceiveRejectedStatusNotification(IModularRpcRemoteConnection connection, ulong id, ESteamConnectionFailureInfo rejection, string? reason, uint duration)
    {
        CSteamID steamId = new CSteamID(id);
        if (!Dummies.TryGetValue(steamId.m_SteamID, out RemoteDummyPlayerActor actor))
            throw new ArgumentException($"Player {id} not found.");

        actor.Status = DummyReadyStatus.InMenu;
        actor.RejectReason = rejection;
        actor.RejectReasonString = reason;
        actor.RejectDuration = duration == 0 ? null : TimeSpan.FromSeconds(duration);
        actor.ReadyCondition?.TrySetResult(actor);
        _logger.LogInformation(
            $"Remote actor status update after rejection: {actor.Steam64.m_SteamID} (PID {actor.ProcessId}): {nameof(DummyReadyStatus.InMenu)} ({rejection})."
        );
    }

    [RpcReceive]
    private async Task ReceiveStatusNotification(IModularRpcRemoteConnection connection, ulong id, DummyReadyStatus status, nint hwnd)
    {
        CSteamID steamId = new CSteamID(id);
        if (!Dummies.TryGetValue(steamId.m_SteamID, out RemoteDummyPlayerActor actor))
            throw new ArgumentException($"Player {id} not found.");

        actor.Status = status;
        actor.RejectReason = null;
        actor.RejectReasonString = null;
        actor.RejectDuration = null;
        if (status == DummyReadyStatus.StartedUp)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                actor.WindowHandle = hwnd;
            }
            Interlocked.CompareExchange(ref actor.ConnectionIntl, connection, null);
            TaskCompletionSource<RemoteDummyPlayerActor>? tcs = actor.LoadedCondition;
            if (tcs == null)
            {
                await Task.Delay(500);
                tcs = actor.LoadedCondition;
            }

            if (tcs == null || !tcs.TrySetResult(actor))
                throw new ArgumentException($"Player {id} already started.");
        }
        else if (status == DummyReadyStatus.InMenu)
        {
            actor.ReadyCondition?.TrySetResult(actor);
        }

        _logger.LogInformation($"Remote actor status update: {actor.Steam64.m_SteamID} (PID {actor.ProcessId}): {status}.");
    }

    /// <summary>
    /// For some reason players can get stuck in queue so this re-calls verifyNextPlayerInQueue().
    /// </summary>
    [RpcReceive]
    private async Task ReceiveInQueueBump(ulong steam64)
    {
        if (!Dummies.TryGetValue(steam64, out RemoteDummyPlayerActor? player))
        {
            return;
        }

        int queueBumpVersion = Interlocked.Increment(ref player.QueueBumpVersion);
        _logger.LogTrace($"Received in-queue notification from player \"{player.DisplayName}\" (PID {player.ProcessId}).");
        await Task.Delay(2500);
        await GameThread.Switch();
        if (player.IsOnline)
            return;

        if (Provider.pending.Count > 0 && Provider.pending[0].playerID.steamID.m_SteamID == steam64 && queueBumpVersion == player.QueueBumpVersion)
        {
            _logger.LogWarning($"Player \"{player.DisplayName}\" (PID {player.ProcessId}) did not exit the queue in a reasonable amount of time, attempting to re-verify.");
            Provider.verifyNextPlayerInQueue();
        }
        else if (queueBumpVersion == player.QueueBumpVersion)
        {
            _logger.LogWarning($"Player \"{player.DisplayName}\" (PID {player.ProcessId}) did not exit the queue in a reasonable amount of time, but is not at the end of the queue.");
        }
        else
        {
            _logger.LogTrace($"Player \"{player.DisplayName}\" (PID {player.ProcessId}) in-queue notification was replaced by a new one.");
        }
    }

    public async Task<bool> StartDummiesAsync(int amt)
    {
        if (amt <= 0)
        {
            return true;
        }

        nint display = 0;
        bool didTileConsole = false;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint window = RemoteDummyWindowsManager.GetWindowHandle(true);
            if (window != 0)
            {
                display = RemoteDummyWindowsManager.GetMonitorHandle(window);
                if (RemoteDummyWindowsManager.AlignWindowToGrid(display, window, 0, amt + 1, out bool isPrimaryMonitor))
                {
                    _logger.LogTrace(isPrimaryMonitor
                        ? $"Tiled console window (HWND 0x{(long)window:X16}) to primary monitor."
                        : $"Tiled console window (HWND 0x{(long)window:X16}).");
                    didTileConsole = true;
                }
                else
                    _logger.LogWarning("Failed to tile server console window.");
            }
            else
            {
                _logger.LogWarning("Failed to find console window handle.");
            }

            if (display == 0)
            {
                display = RemoteDummyWindowsManager.GetPrimaryMonitorHandle();
            }
        }

        string pipeName = $"{NamedPipe.PipeName}_{Provider.serverName}";

        NamedPipeEndpoint newEndpoint = NamedPipeEndpoint.AsServer(ModularRpcsServices, pipeName);
        NamedPipeEndpoint? oldEndpoint = Interlocked.Exchange(ref _rpcServer, newEndpoint);
        if (oldEndpoint != null)
        {
            await oldEndpoint.CloseServerAsync();
        }

        await newEndpoint.CreateServerAsync().ConfigureAwait(false);

        InstallDirUtility dirUtil = new InstallDirUtility(false, _logger);
        if (_module.TestList is { ClientInstallDir: { Length: > 0 } installDir })
        {
            dirUtil.OverrideInstallDirectory = installDir;
        }

        JsonSerializer serializer = JsonSerializer.CreateDefault();

        // cmd line args don't support escaping backslashes in Unturned so we have to explicitly use forward slashes
        string baseDummyPath = Path.Combine(_module.HomeDirectory, "Dummies/");

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(baseDummyPath))
            {
                if (uint.TryParse(Path.GetFileName(dir), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
        catch (DirectoryNotFoundException) { }

        string? assetConfig = Path.Combine(_module.HomeDirectory, "asset-load.json");
        AssetLoadModel? assetLoadModel = _module.AssetLoadModel;
        if (assetLoadModel != null)
        {
            try
            {
                using JsonTextWriter jsonWriter = new JsonTextWriter(new StreamWriter(assetConfig, false, Encoding.UTF8));
                jsonWriter.Formatting = Formatting.None;
                assetLoadModel.WriteToJson(jsonWriter);
                jsonWriter.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to write asset config to {assetConfig}:{System.Environment.NewLine}{ex}.");
            }
        }
        else
        {
            try
            {
                File.Delete(assetConfig);
            }
            catch (FileNotFoundException) { }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to delete asset config at {assetConfig}:{System.Environment.NewLine}{ex}.");
            }

            assetConfig = null;
        }

        if (Path.DirectorySeparatorChar == '\\')
            baseDummyPath = baseDummyPath.Replace('\\', '/');

        ulong[] workshopIds = WorkshopDownloadConfig.getOrLoad().File_IDs.ToArray();

        bool failed = false;

        RemoteDummyPlayerActor[] actors = new RemoteDummyPlayerActor[amt];
        string moduleDir = _module.HomeDirectory.Replace('\\', '/');
        for (int i = 0; i < amt; ++i)
        {
            CSteamID steamId =  _module.Dummies.SteamIdPool.GetUniqueCSteamID();

            string steamName = $"Dummy ({(steamId.GetAccountID().m_AccountID / SteamIdPool.FinalDigitsFactor).ToString("X8", CultureInfo.InvariantCulture)})";

            string configDir = baseDummyPath + steamId.GetAccountID().m_AccountID.ToString("X8", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(configDir);

            string args = GetArgs(steamId, moduleDir, configDir);

            using (JsonTextWriter writer = new JsonTextWriter(new StreamWriter(configDir + "/startup.json", false, Encoding.UTF8)))
            {
                writer.CloseOutput = true;
#if DEBUG
                writer.Formatting = Formatting.Indented;
                writer.IndentChar = ' ';
                writer.Indentation = 4;
#else
                writer.Formatting = Formatting.None;
#endif

                serializer.Serialize(writer, new DummyLauncherConfig
                {
                    Steam64 = steamId.m_SteamID,
                    WorkshopIds = workshopIds,
                    Name = steamName,
                    PipeName = pipeName,
                    Index = i,
                    Count = amt,
                    DisplayHandle = display == 0 ? null : display,
                    TileOffset = didTileConsole ? 1 : 0,
                    AssetConfig = assetConfig
                });
            }

            string unturnedInstall = dirUtil.InstallDirectory;
            string exe = Path.Combine(unturnedInstall, dirUtil.GetExecutableRelativePath());

            Process process = new Process();
            process.StartInfo.FileName = exe;
            process.StartInfo.Arguments = args;
            process.StartInfo.WorkingDirectory = unturnedInstall;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            try
            {
                process.StartInfo.UseShellExecute = false;
            }
            catch (PlatformNotSupportedException) { }

            RemoteDummyPlayerActor? actor = null;
            try
            {
                actor = ProxyGenerator.Instance.CreateProxy<RemoteDummyPlayerActor>(
                    ModularRpcsServices.GetRequiredService<IRpcRouter>(),
                    nonPublic: true,
                    steamId,
                    steamName,
                    this,
                    process,
                    Path.GetFullPath(configDir),
                    -1
                );

                actor.LogFileWriter = new StreamWriter(configDir + "/Unity.log");
                process.OutputDataReceived += actor.HandleOutputDataReceived;
                process.ErrorDataReceived += actor.HandleOutputDataReceived;

                _logger.LogInformation($"Starting simulated player {i}...");
                if (!process.Start())
                {
                    _logger.LogError($"Failed to start process for actor {i} {steamId.m_SteamID}.");
                    process.Dispose();
                    failed = true;
                    actor.Dispose();
                    actor = null;
                }
                else
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    actor.ProcessId = (uint)process.Id;

                    actors[i] = actor;

                    if (!Dummies.TryAdd(steamId.m_SteamID, actor))
                    {
                        throw new Exception("Duplicate steam ID, this shouldn't happen.");
                    }

                    _logger.LogInformation($"Waiting to hear from simulated player {i} (PID {actor.ProcessId})...");
                    await Task.WhenAny(
                        Task.Delay(TimeSpan.FromSeconds(15), _module.CancellationToken),
                        actor.LoadedCondition!.Task
                    );
                    if (!actor.LoadedCondition.Task.IsCompleted || process.HasExited)
                    {
                        _logger.LogWarning($"Still waiting after 15 seconds for simulated player {i} (PID {actor.ProcessId}) to start loading (or it was closed).");

                        if (!process.HasExited)
                        {
                            await Task.WhenAny(
                                Task.Delay(TimeSpan.FromSeconds(45), _module.CancellationToken),
                                actor.LoadedCondition.Task
                            );
                        }
                    }

                    if (!actor.LoadedCondition.Task.IsCompleted || process.HasExited)
                    {
                        _logger.LogError($"Simulated player {i} (PID {actor.ProcessId}) did not start loading within a minute (or was closed).");
                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to kill process for client {i}: {actor.ProcessId} ({actor.Steam64.m_SteamID}).", ex);
                        }
                        finally
                        {
                            process.Dispose();
                        }

                        failed = true;
                    }
                    else
                    {
                        await actor.LoadedCondition.Task;
                        actor.LoadedCondition = null;
                        _logger.LogInformation($"Simulated player {i} (PID {actor.ProcessId}) has started loading.");
                    }
                }
            }
            catch
            {
                Dummies.TryRemove(steamId.m_SteamID, out _);
                process.Dispose();
                actor?.Dispose();
                throw;
            }

            if (!failed)
                continue;

            for (int j = 0; j < i; ++j)
            {
                RemoteDummyPlayerActor a = actors[j];
                try
                {
                    a.Process.Kill();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to kill process for client {j}: {a.ProcessId} ({a.Steam64.m_SteamID}).", ex);
                }
                finally
                {
                    a.Process.Dispose();
                }
                Dummies.TryRemove(a.Steam64.m_SteamID, out _);
                a.Dispose();
            }

            break;
        }

        return !failed;
    }

    /// <inheritdoc />
    public Task SpawnPlayerAsync(IServersideTestPlayer player, Action<DummyPlayerJoinConfiguration>? configurePlayers, bool ignoreAlreadyConnected, CancellationToken token)
    {
        RemoteDummyPlayerActor actor = (RemoteDummyPlayerActor)player;

        DummyManager dummyManager = _module.Dummies;
        dummyManager.EnsureServerDetailsCached();

        return ConnectDummyAsync(actor, token, dummyManager.ServerDetailsAddress.value, dummyManager.ServerDetailsPort, dummyManager.ServerDetailsCode, ignoreAlreadyConnected, configurePlayers);
    }

    /// <inheritdoc />
    public Task DespawnPlayerAsync(IServersideTestPlayer player, bool ignoreAlreadyDisconnected, CancellationToken token)
    {
        RemoteDummyPlayerActor actor = (RemoteDummyPlayerActor)player;
        return DisconnectDummyAsync(ignoreAlreadyDisconnected, actor);
    }

    private async Task ConnectDummyAsync(RemoteDummyPlayerActor actor, CancellationToken token, uint ipv4, ushort port, ulong serverCode, bool ignoreError, Action<DummyPlayerJoinConfiguration>? configurePlayer)
    {
        if (actor.Status is DummyReadyStatus.Connected or DummyReadyStatus.Connecting)
        {
            TaskCompletionSource<RemoteDummyPlayerActor>? connectedCondition = actor.ConnectedCondition;
            if (ignoreError)
            {
                if (connectedCondition != null)
                {
                    await connectedCondition.Task;
                }
                return;
            }

            throw new InvalidOperationException($"Actor {actor.Steam64.m_SteamID} is already connected or connecting.");
        }

        TaskCompletionSource<RemoteDummyPlayerActor>? readyTask = actor.ReadyCondition;
        if (readyTask != null)
        {
            if (!readyTask.Task.IsCompleted)
            {
                _logger.LogInformation($"[uTest] Waiting for {actor.Steam64} (PID {actor.ProcessId}) to finish loading assets before connecting.");

                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromMinutes(5d), token),
                    readyTask.Task
                );
            }
            if (!readyTask.Task.IsCompleted)
                throw new TimeoutException($"Timed out waiting for {actor.Steam64} (PID {actor.ProcessId}) to finish loading assets after 5 minutes.");

            await readyTask.Task;
        }

        TaskCompletionSource<RemoteDummyPlayerActor> connectTask = new TaskCompletionSource<RemoteDummyPlayerActor>();
        actor.ConnectedCondition = connectTask;
        actor.ReadyCondition = new TaskCompletionSource<RemoteDummyPlayerActor>();

        await GameThread.Switch(token);
        actor.Configure(configurePlayer);

        _logger.LogInformation($"[uTest] Notifying dummy {actor.Steam64.m_SteamID} to connect to server.");
        actor.Status = DummyReadyStatus.Connecting;
        Interlocked.Increment(ref actor.QueueBumpVersion);
        bool hasPassword = !string.IsNullOrEmpty(Provider.serverPassword);

        string password = Provider.serverPassword;
        if (hasPassword && !actor.Configuration.UseCorrectPassword)
        {
            if (password.Length > 1)
                password = password[1..];
            else
                password += password;
        }

        ulong[] equippedSkins = actor.Configuration.GetEquippedSkinsArray(out string[] skinTags, out string[] skinDynamicProps);

        actor.SkinTags = skinTags;
        actor.SkinDynamicProps = skinDynamicProps;

        await actor.Connect(ipv4, port, password, serverCode,
            Provider.map,
            Provider.cameraMode,
            Provider.isPvP,
            "Unturned.uTest",
            Provider.hasCheats,
            Provider.getServerWorkshopFileIDs().Count > 0,
            Provider.mode,
            Provider.clients.Count,
            Provider.maxPlayers,
            hasPassword,
            Provider.isGold,
            Provider.configData.Browser.Is_Using_Anycast_Proxy ? SteamServerAdvertisement.EAnycastProxyMode.TaggedByHost : SteamServerAdvertisement.EAnycastProxyMode.None,
            Provider.configData.Browser.Monetization,
            Provider.configData.Server.VAC_Secure,
            Provider.IsBattlEyeActiveOnCurrentServer,
            SteamPluginAdvertising.Get().PluginFrameworkTag switch
            {
                null or "" => SteamServerAdvertisement.EPluginFramework.None,
                "rm" => SteamServerAdvertisement.EPluginFramework.Rocket,
                "om" => SteamServerAdvertisement.EPluginFramework.OpenMod,
                _ => SteamServerAdvertisement.EPluginFramework.Unknown
            },
            Provider.configData.Browser.Thumbnail,
            Provider.configData.Browser.Desc_Server_List,
            actor.HWIDs,
            actor.Configuration.CharacterName,
            actor.Configuration.NickName,
            actor.Configuration.CharacterIndex,
            unchecked ( (ulong)actor.Configuration.ShirtItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.PantsItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.HatItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.BackpackItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.VestItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.MaskItem.m_SteamItemDef ),
            unchecked ( (ulong)actor.Configuration.GlassesItem.m_SteamItemDef ),
            actor.Configuration.SteamGroupId.m_SteamID,
            actor.Configuration.SteamLobbyId.m_SteamID,
            actor.Configuration.FaceIndex,
            actor.Configuration.HairIndex,
            actor.Configuration.BeardIndex,
            actor.Configuration.SkinColor,
            actor.Configuration.HairColor,
            actor.Configuration.MarkerColor,
            actor.Configuration.BeardColor,
            actor.Configuration.Platform,
            actor.Configuration.ReportedPing,
            actor.Configuration.IsLeftHanded,
            equippedSkins,
            actor.Configuration.Skillset,
            actor.Configuration.GetRequiredModulesString(),
            actor.Configuration.Language,
            actor.Configuration.ReportedGameVersion == null ? null : Parser.getUInt32FromIP(actor.Configuration.ReportedGameVersion),
            actor.Configuration.ReportedMapVersion == null ? null : Parser.getUInt32FromIP(actor.Configuration.ReportedMapVersion),
            actor.Configuration.UseCorrectGameVersion,
            actor.Configuration.UseCorrectMapVersion,
            actor.Configuration.UseCorrectLevelHash,
            actor.Configuration.UseCorrectAssemblyHash,
            actor.Configuration.UseCorrectResourceHash,
            actor.Configuration.UseCorrectEconHash
        );

        await Task.WhenAny(
            Task.Delay(TimeSpan.FromMinutes(2d), token),
            connectTask.Task,
            actor.ReadyCondition.Task
        );
        
        if (actor.Status == DummyReadyStatus.InMenu)
        {
            throw new ActorDestroyedException(actor, Properties.Resources.ActorDestroyedDisconnectedWhileConnecting);
        }
        
        if (!connectTask.Task.IsCompleted)
            throw new TimeoutException($"Timed out waiting for {actor.Steam64} (PID {actor.ProcessId}) to connect after 2 minutes.");

        await connectTask.Task;
        actor.ConnectedCondition = null;
        actor.Status = DummyReadyStatus.Connected;
    }

    private async Task DisconnectDummyAsync(bool ignoreError, RemoteDummyPlayerActor actor)
    {
        if (actor.Status is not DummyReadyStatus.Connected and not DummyReadyStatus.Connecting)
        {
            if (ignoreError)
                return;

            throw new InvalidOperationException($"Dummy {actor.Steam64.m_SteamID} not connected.");
        }

        TaskCompletionSource<RemoteDummyPlayerActor> newTcs = new TaskCompletionSource<RemoteDummyPlayerActor>();
        TaskCompletionSource<RemoteDummyPlayerActor> rdyCond = Interlocked.CompareExchange(ref actor.ReadyCondition, newTcs, null) ?? newTcs;
        if (rdyCond.Task.IsCompleted)
        {
            TaskCompletionSource<RemoteDummyPlayerActor> oldRdyCond = Interlocked.CompareExchange(ref actor.ReadyCondition, newTcs, rdyCond);
            rdyCond = oldRdyCond == rdyCond ? newTcs : oldRdyCond;
        }

        actor.Status = DummyReadyStatus.Disconnecting;
        _logger.LogInformation($"[uTest] Notifying dummy {actor.Steam64.m_SteamID} to disconnect from server.");
        Task dismiss = GameThread.RunAndWaitAsync(actor.Steam64, Provider.dismiss);
        await actor.Disconnect();
        await dismiss;
        await rdyCond.Task;
        Interlocked.CompareExchange(ref actor.ReadyCondition, null, rdyCond);
    }

    internal void CloseAllDummies()
    {
        RemoteDummyPlayerActor[] actors = Dummies.Values.ToArray();
        Dummies.Clear();
        Task[] tasks = new Task[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            RemoteDummyPlayerActor actor = actors[i];
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    bool closed = actor.Process.HasExited;
                    if (actor.ConnectionIntl != null && !closed)
                    {
                        await actor.SendGracefullyClose();
                        closed = actor.Process.WaitForExit(2000);
                    }

                    if (!closed)
                    {
                        actor.Process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[uTest] Error gracefully closing connection with simulated dummy {actor.Steam64}.", ex);
                }
                finally
                {
                    actor.Process.Dispose();
                    actor.Dispose();
                }
            });
        }

        Task.WhenAll(tasks).Wait();

        Interlocked.Exchange(ref _rpcServer, null)?.CloseServerAsync();
    }
}

public enum DummyReadyStatus : byte
{
    /// <summary>
    /// Client has not yet booted to the loading screen.
    /// </summary>
    Unknown,

    /// <summary>
    /// Invoked after <see cref="IModuleNexus.initialize"/>.
    /// </summary>
    StartedUp,

    /// <summary>
    /// Invoked after vanilla and subscribed workshop assets have been loaded, therefore client is ready to connect.
    /// </summary>
    InMenu,

    /// <summary>
    /// Client is in the process of disconnecting from a server, after which they will return to the <see cref="InMenu"/> status.
    /// </summary>
    Disconnecting,
    
    /// <summary>
    /// Client is in the process of connecting to the server.
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Client is connected to the server.
    /// </summary>
    Connected
}

internal class DummyLauncherConfig
{

    public required ulong Steam64 { get; set; }
    public required string Name { get; set; }
    public required string PipeName { get; set; }
    public required string? AssetConfig { get; set; }
    public required int Index { get; set; }
    public required int Count { get; set; }
    public required long? DisplayHandle { get; set; }
    public required int TileOffset { get; set; }
    public required ulong[] WorkshopIds { get; set; }
}