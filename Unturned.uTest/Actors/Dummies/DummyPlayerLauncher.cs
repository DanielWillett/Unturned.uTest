using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unturned.SystemEx;
using uTest.Module;
using uTest.Protocol.DummyPlayerHost;
// ReSharper disable LocalizableElement

namespace uTest.Dummies;

/// <summary>
/// Handles launching fully simulated dummy clients.
/// </summary>
[GenerateRpcSource]
internal partial class DummyPlayerLauncher : IDummyPlayerController
{
    private readonly MainModule _module;
    private readonly ILogger _logger;

    private readonly Dictionary<ulong, RemoteDummyPlayerActor> _remoteDummies = new Dictionary<ulong, RemoteDummyPlayerActor>();

    internal SteamIdPool SteamIdPool { get; }

    internal IServiceProvider ModularRpcsServices { get; private set; }

    public DummyPlayerLauncher(MainModule module, ILogger logger, IServiceProvider serviceProvider)
    {
        _module = module;
        _logger = logger;
        SteamIdPool = new SteamIdPool(module.TestList?.SteamIdGenerationStyle ?? SteamIdGenerationStyle.DevUniverse);
        ModularRpcsServices = serviceProvider;
    }

    public bool TryGetRemoteDummy(Player player, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return TryGetRemoteDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetRemoteDummy(ulong steam64, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return _remoteDummies.TryGetValue(steam64, out dummy);
    }

    private string GetArgs(CSteamID steamId, string dataDir, int i)
    {
        return (i == 0 ? string.Empty : "-NoDefaultLog ") +
               $"-ModulesPath \"{_module.HomeDirectory}\" " +
               $"-uTestSteamId {steamId.m_SteamID.ToString(CultureInfo.InvariantCulture)} " +
               $"-uTestDataDir {CommandLineHelper.EscapeCommandLineArg(dataDir)} " +
                "-NoVanillaAssemblySearch " +
                "-NoWorkshopSubscriptions";
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
                await SendWorkshopItemsUpdate(_remoteDummies.Values
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

    [RpcReceive, UsedImplicitly]
    private async Task ReceiveStatusNotification(IModularRpcRemoteConnection connection, ulong id, DummyReadyStatus status)
    {
        CSteamID steamId = new CSteamID(id);
        if (!_remoteDummies.TryGetValue(steamId.m_SteamID, out RemoteDummyPlayerActor actor))
            throw new ArgumentException($"Player {id} not found.");

        actor.Status = status;
        if (status == DummyReadyStatus.StartedUp)
        {
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
        else if (status == DummyReadyStatus.AssetsLoaded)
        {
            actor.ReadyCondition?.TrySetResult(actor);
        }

        _logger.LogInformation($"Remote actor status update: {actor.Steam64.m_SteamID} (PID {actor.ProcessId}): {status}.");
    }

    public async Task<bool> StartDummiesAsync(int amt)
    {
        NamedPipeEndpoint endpoint = NamedPipeEndpoint.AsServer(ModularRpcsServices, NamedPipe.PipeName);
        await endpoint.CreateServerAsync().ConfigureAwait(false);

        InstallDirUtility dirUtil = new InstallDirUtility(false, _logger);
        if (_module.TestList is { ClientInstallDir: { Length: > 0 } installDir })
        {
            dirUtil.OverrideInstallDirectory = installDir;
        }

        JsonSerializer serializer = JsonSerializer.CreateDefault();

        // cmd line args don't support escaping backslashes in Unturend so we have to explicitly use forward slashes
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

        if (Path.DirectorySeparatorChar == '\\')
            baseDummyPath = baseDummyPath.Replace('\\', '/');

        ulong[] workshopIds = WorkshopDownloadConfig.getOrLoad().File_IDs.ToArray();

        bool failed = false;

        RemoteDummyPlayerActor[] actors = new RemoteDummyPlayerActor[amt];

        for (int i = 0; i < amt; ++i)
        {
            CSteamID steamId = SteamIdPool.GetUniqueCSteamID();

            string configDir = baseDummyPath + steamId.GetAccountID().m_AccountID.ToString("X8", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(configDir);

            string args = GetArgs(steamId, configDir, i);

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
                    WorkshopIds = workshopIds
                });
            }

            string unturnedInstall = dirUtil.InstallDirectory;
            string exe = Path.Combine(unturnedInstall, dirUtil.GetExecutableRelativePath());

            ProcessStartInfo startInfo = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = unturnedInstall,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            try
            {
                startInfo.UseShellExecute = false;
            }
            catch (PlatformNotSupportedException) { }

            Process? process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError($"Failed to start process for client {i} ({steamId.m_SteamID}).");
                failed = true;
            }
            else
            {
                try
                {
                    RemoteDummyPlayerActor actor = ProxyGenerator.Instance.CreateProxy<RemoteDummyPlayerActor>(
                        ModularRpcsServices.GetRequiredService<IRpcRouter>(),
                        nonPublic: true,
                        steamId,
                        $"TestPlayer_{steamId.GetAccountID().m_AccountID}",
                        this,
                        process,
                        Path.GetFullPath(configDir)
                    );

                    actors[i] = actor;

                    _remoteDummies.Add(steamId.m_SteamID, actor);
                    _logger.LogInformation($"Waiting to hear from simulated player {i} (PID {actor.ProcessId})...");
                    await Task.WhenAny(
                        Task.Delay(TimeSpan.FromSeconds(15), _module.CancellationToken),
                        actor.LoadedCondition!.Task
                    );
                    if (!actor.LoadedCondition.Task.IsCompleted || process.HasExited)
                    {
                        _logger.LogWarning($"Still waiting after 15 seconds for simulated player {i} (PID {actor.ProcessId}) to start loading (or was closed).");

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
                catch
                {
                    _remoteDummies.Remove(steamId.m_SteamID);
                    process.Dispose();
                    throw;
                }
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
                _remoteDummies.Remove(a.Steam64.m_SteamID);
            }

            break;
        }

        return !failed;
    }

    public ValueTask ConnectDummyPlayersAsync(List<ulong>? idsOrNull, CancellationToken token)
    {
        if (_remoteDummies.Count > 0)
        {
            return new ValueTask(ConnectDummyPlayersAsyncIntl(idsOrNull, token));
        }

        return default;
    }

    public ValueTask DisconnectDummyPlayersAsync(List<ulong>? idsOrNull, CancellationToken token)
    {
        if (_remoteDummies.Count > 0)
        {
            return new ValueTask(DisconnectDummyPlayersAsyncIntl(idsOrNull, token));
        }

        return default;
    }

    private async Task ConnectDummyPlayersAsyncIntl(List<ulong>? idsOrNull, CancellationToken token)
    {
        await GameThread.Switch(token);

        ulong serverCode = SteamGameServer.GetSteamID().m_SteamID;
        ushort port = Provider.GetServerConnectionPort();

        if (!IPv4Address.TryParse(Provider.bindAddress, out IPv4Address address))
        {
            address = new IPv4Address(127u << 24 | 1 /* 127.0.0.1 */);
        }

        if (idsOrNull == null)
        {
            foreach (RemoteDummyPlayerActor actor in _remoteDummies.Values)
            {
                await ConnectDummyAsync(actor, token, address.value, port, serverCode);
            }
        }
        else
        {
            foreach (ulong id in idsOrNull)
            {
                if (!_remoteDummies.TryGetValue(id, out RemoteDummyPlayerActor actor))
                    throw new ArgumentException($"Fully-simulated dummy doesn't exist: {id}.", nameof(idsOrNull));

                await ConnectDummyAsync(actor, token, address.value, port, serverCode);
            }
        }
    }
    
    private async Task DisconnectDummyPlayersAsyncIntl(List<ulong>? idsOrNull, CancellationToken token)
    {
        await GameThread.Switch(token);

        if (idsOrNull == null)
        {
            foreach (RemoteDummyPlayerActor actor in _remoteDummies.Values)
            {
                await DisconnectDummyAsync(actor);
            }
        }
        else
        {
            foreach (ulong id in idsOrNull)
            {
                if (!_remoteDummies.TryGetValue(id, out RemoteDummyPlayerActor actor))
                    throw new ArgumentException($"Fully-simulated dummy doesn't exist: {id}.", nameof(idsOrNull));

                await DisconnectDummyAsync(actor);
            }
        }
    }

    private async Task ConnectDummyAsync(RemoteDummyPlayerActor actor, CancellationToken token, uint ipv4, ushort port, ulong serverCode)
    {
        TaskCompletionSource<RemoteDummyPlayerActor>? readyTask = actor.ReadyCondition;
        if (readyTask != null)
        {
            if (!readyTask.Task.IsCompleted)
            {
                _logger.LogInformation($"Waiting for {actor.Steam64} (PID {actor.ProcessId}) to finish loading assets.");

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

        await actor.Connect(ipv4, port, Provider.serverPassword, serverCode,
            Provider.map,
            Provider.cameraMode,
            Provider.isPvP,
            "Unturned.uTest",
            Provider.hasCheats,
            Provider.getServerWorkshopFileIDs().Count > 0,
            Provider.mode,
            Provider.clients.Count,
            Provider.maxPlayers,
            !string.IsNullOrEmpty(Provider.serverPassword),
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
            Provider.configData.Browser.Desc_Server_List
        );

        await Task.WhenAny(
            Task.Delay(TimeSpan.FromMinutes(2d), token),
            connectTask.Task
        );
        
        if (!connectTask.Task.IsCompleted)
            throw new TimeoutException($"Timed out waiting for {actor.Steam64} (PID {actor.ProcessId}) to connect after 2 minutes.");

        await connectTask.Task;
        actor.ConnectedCondition = null;
        actor.Status = DummyReadyStatus.Connected;
    }

    private static async Task DisconnectDummyAsync(RemoteDummyPlayerActor actor)
    {
        if (actor.ReadyCondition is { Task.IsCompleted: false })
            return;

        await actor.Disconnect();
        actor.Status = DummyReadyStatus.AssetsLoaded;
    }

    internal void CloseAllDummies()
    {
        List<RemoteDummyPlayerActor> actors = _remoteDummies.Values.ToList();
        Task[] tasks = new Task[actors.Count];
        for (int i = 0; i < actors.Count; i++)
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
                        closed = actor.Process.WaitForExit(1000);
                    }

                    if (!closed)
                    {
                        actor.Process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error gracefully closing connection with simulated dummy {actor.Steam64}.", ex);
                }
                finally
                {
                    actor.Process.Dispose();
                }
            });
        }

        Task.WhenAll(tasks).Wait();
    }
}

internal enum DummyReadyStatus : byte
{
    /// <summary>
    /// Invoked after <see cref="IModuleNexus.initialize"/>.
    /// </summary>
    StartedUp,

    /// <summary>
    /// Invoked after vanilla and workshop assets have been loaded, therefore client is ready to connect.
    /// </summary>
    AssetsLoaded,

    Connected
}

internal class DummyLauncherConfig
{

    public required ulong Steam64 { get; set; }
    public required ulong[] WorkshopIds { get; set; }
}