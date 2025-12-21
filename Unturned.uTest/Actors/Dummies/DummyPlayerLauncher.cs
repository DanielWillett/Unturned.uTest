using DanielWillett.ModularRpcs;
using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.Configuration;
using DanielWillett.ModularRpcs.DependencyInjection;
using DanielWillett.ModularRpcs.NamedPipes;
using DanielWillett.ModularRpcs.Reflection;
using DanielWillett.ModularRpcs.Routing;
using DanielWillett.ModularRpcs.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using uTest.Module;
using uTest.Protocol.DummyPlayerHost;

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

    public DummyPlayerLauncher(MainModule module, ILogger logger)
    {
        _module = module;
        _logger = logger;
        SteamIdPool = new SteamIdPool(module.TestList?.SteamIdGenerationStyle ?? SteamIdGenerationStyle.DevUniverse);
        ServiceContainer c = new ServiceContainer();

        ProxyGenerator.Instance.SetLogger(UnturnedLogLogger.Instance);
        ServerRpcConnectionLifetime lifetime = new ServerRpcConnectionLifetime();
        DefaultSerializer serializer = new DefaultSerializer(new SerializationConfiguration
        {
            MaximumGlobalArraySize = 256,
            MaximumArraySizes = { { typeof(byte), 16384 } }
        });
        DependencyInjectionRpcRouter router = new DependencyInjectionRpcRouter(c);
        lifetime.SetLogger(UnturnedLogLogger.Instance);
        router.SetLogger(UnturnedLogLogger.Instance);

        c.AddService(typeof(ProxyGenerator), ProxyGenerator.Instance);
        c.AddService(typeof(IRpcConnectionLifetime), lifetime);
        c.AddService(typeof(IRpcSerializer), serializer);
        c.AddService(typeof(IRpcRouter), router);
        c.AddService(typeof(DummyPlayerLauncher), this);

        ModularRpcsServices = c;
    }

    public bool TryGetRemoteDummy(Player player, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return TryGetRemoteDummy(player.channel.owner.playerID.steamID.m_SteamID, out dummy);
    }

    public bool TryGetRemoteDummy(ulong steam64, [MaybeNullWhen(false)] out RemoteDummyPlayerActor dummy)
    {
        return _remoteDummies.TryGetValue(steam64, out dummy);
    }

    private string GetArgs(CSteamID steamId, string dataDir)
    {
        return $"-NoDefaultLog " +
               $"-ModulesPath \"{_module.HomeDirectory}\" " +
               $"-uTestSteamId {steamId.m_SteamID.ToString(CultureInfo.InvariantCulture)} " +
               $"-uTestDataDir {CommandLineHelper.EscapeCommandLineArg(dataDir)} " +
               $"-NoVanillaAssemblySearch " +
               $"-NoWorkshopSubscriptions";
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

    [RpcSend("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost", "ReceiveWorkshopItemsUpdate"), UsedImplicitly]
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
            TaskCompletionSource<RemoteDummyPlayerActor>? tcs = actor._loadTcs;
            if (tcs == null)
            {
                await Task.Delay(500);
            }

            if (tcs == null || !tcs.TrySetResult(actor))
                throw new ArgumentException($"Player {id} already started.");
        }
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

        string baseDummyPath = Path.Combine(_module.HomeDirectory, "Dummies/");
        if (Path.PathSeparator == '\\')
            baseDummyPath = baseDummyPath.Replace(Path.PathSeparator, '/');

        ulong[] workshopIds = WorkshopDownloadConfig.getOrLoad().File_IDs.ToArray();

        bool failed = false;

        RemoteDummyPlayerActor[] actors = new RemoteDummyPlayerActor[amt];
        for (int i = 0; i < amt; ++i)
        {
            CSteamID steamId = SteamIdPool.GetUniqueCSteamID();

            // cmd line args don't support escaping backslashes in Unturend so we have to explicitly use forward slashes
            string configDir = baseDummyPath + steamId.GetAccountID().m_AccountID.ToString("X8", CultureInfo.InvariantCulture);
            
            string args = GetArgs(steamId, configDir);

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

            Process? p = Process.Start(startInfo);
            if (p == null)
            {
                _logger.LogError($"Failed to start process for client {i} ({steamId.m_SteamID}).");
                failed = true;
                for (int j = 0; j < i; ++j)
                {
                    RemoteDummyPlayerActor a = actors[j];
                    try
                    {
                        a.Process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to kill process for client {j} ({a.Steam64.m_SteamID}).", ex);
                    }
                    a.Process.Dispose();
                    _remoteDummies.Remove(a.Steam64.m_SteamID);
                }
                
                break;
            }

            try
            {
                RemoteDummyPlayerActor actor = new RemoteDummyPlayerActor(steamId, $"TestPlayer_{steamId.GetAccountID().m_AccountID}", this, p);
                actors[i] = actor;

                _remoteDummies.Add(steamId.m_SteamID, actor);
                actor.ProcessId = (uint)p.Id;
                actor.Process = p;

                actor._loadTcs = new TaskCompletionSource<RemoteDummyPlayerActor>();
                await actor._loadTcs.Task;
            }
            catch
            {
                _remoteDummies.Remove(steamId.m_SteamID);
                p.Dispose();
                throw;
            }
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

    private Task ConnectDummyPlayersAsyncIntl(List<ulong>? idsOrNull, CancellationToken token)
    {
        return Task.CompletedTask;
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
    AssetsLoaded
}

internal class DummyLauncherConfig
{

    public required ulong Steam64 { get; set; }
    public required ulong[] WorkshopIds { get; set; }
}