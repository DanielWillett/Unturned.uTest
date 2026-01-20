using DanielWillett.ModularRpcs.Abstractions;
using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using DanielWillett.ModularRpcs.Protocol;
using System;
using System.Diagnostics;
using System.IO;

namespace uTest.Dummies;

/// <summary>
/// A dummy player which is ran as an instance of Unturned connecting over <c>localhost</c>.
/// Usually <see cref="SimulatedDummyPlayerActor"/> players can be used instead with much less overhead.
/// </summary>
[GenerateRpcSource]
public sealed partial class RemoteDummyPlayerActor : BaseServersidePlayerActor, IRpcSingleConnectionObject
{
    internal TaskCompletionSource<RemoteDummyPlayerActor>? LoadedCondition = new TaskCompletionSource<RemoteDummyPlayerActor>();
    internal TaskCompletionSource<RemoteDummyPlayerActor>? ReadyCondition = new TaskCompletionSource<RemoteDummyPlayerActor>();
    internal TaskCompletionSource<RemoteDummyPlayerActor>? ConnectedCondition;
    internal IModularRpcRemoteConnection? ConnectionIntl;
    internal StreamWriter? LogFileWriter;
    internal int QueueBumpVersion;
    internal string[]? SkinTags;
    internal string[]? SkinDynamicProps;

    /// <summary>
    /// The 'home' directory for this client, storing startup config, cloud files, etc.
    /// </summary>
    public string HomeDirectory { get; }

    /// <summary>
    /// The instance of the Unturned client process.
    /// </summary>
    public Process Process { get; internal set; }

    /// <summary>
    /// The PID of the instance of the Unturned client process.
    /// </summary>
    public uint ProcessId { get; internal set; }

    /// <summary>
    /// The Unity window handle ('HWND') if currently on Windows. This is unused on other platforms.
    /// </summary>
    public nint WindowHandle { get; internal set; }

    /// <summary>
    /// The ModularRPCs connection used to communicate with the client.
    /// </summary>
    public IModularRpcRemoteConnection Connection => ConnectionIntl ?? throw new InvalidOperationException("Not yet connected.");

    /// <summary>
    /// The dummy's connection status.
    /// </summary>
    public DummyReadyStatus Status { get; set; }

    public override bool IsRemotePlayer => true;

    /// <inheritdoc />
    internal RemoteDummyPlayerActor(CSteamID steam64, string displayName, RemoteDummyManager dummyLauncher, Process process, string homeDirectory, int index)
        : base(index, steam64, displayName, dummyLauncher)
    {
        Process = process;
        HomeDirectory = homeDirectory;
    }

    internal void HandleOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        try
        {
            LogFileWriter?.WriteLine(e.Data);
        }
        catch (ObjectDisposedException)
        {
            LogFileWriter = null;
        }
        catch (Exception ex)
        {
            UnturnedLog.error("Error writing to client log.");
            UnturnedLog.error(ex);
            Interlocked.Exchange(ref LogFileWriter, null)?.Dispose();
        }
    }

    private protected override void NotifyConnectedIntl(Player player)
    {
        base.NotifyConnectedIntl(player);
        ConnectedCondition?.TrySetResult(this);
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Exchange(ref LogFileWriter, null)?.Dispose();
        base.Dispose(disposing);
    }

    [RpcSend("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost", "ReceiveGracefullyClose")]
    [RpcTimeout(2 * Timeouts.Seconds)]
    internal partial RpcTask SendGracefullyClose();

    [RpcSend("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost", "ReceiveConnect")]
    internal partial RpcTask Connect(
        uint ipv4,
        ushort port,
        string? password,
        ulong serverCode,
        string map,
        ECameraMode cameraMode,
        bool isPvP,
        string name,
        bool hasCheats,
        bool isWorkshop,
        EGameMode mode,
        int currentPlayers,
        int maxPlayers,
        bool hasPassword,
        bool isPro,
        SteamServerAdvertisement.EAnycastProxyMode anycastMode,
        EServerMonetizationTag monetization,
        bool vacSecure,
        bool battleyeSecure,
        SteamServerAdvertisement.EPluginFramework pluginFramework,
        string thumbnailUrl,
        string descText,
        byte[]? hwids,
        string characterName,
        string nickName,
        byte characterIndex,
        ulong shirt,
        ulong pants,
        ulong hat,
        ulong backpack,
        ulong vest,
        ulong mask,
        ulong glasses,
        ulong group,
        ulong lobby,
        byte face,
        byte hair,
        byte beard,
        Color32 skinColor,
        Color32 hairColor,
        Color32 markerColor,
        Color32 beardColor,
        EClientPlatform platform,
        ushort reportedPing,
        bool isLeftHanded,
        ulong[] activeSkins,
        EPlayerSkillset skillset,
        string? requiredModulesString,
        string language,
        uint? gameVersion,
        uint? mapVersion,
        bool correctGameVersion,
        bool correctMapVersion,
        bool correctLevelHash,
        bool correctAssemblyHash,
        bool correctResourceHash,
        bool correctEconHash
    );

    [RpcSend("uTest.Dummies.Host.DummyPlayerHost, Unturned.uTest.DummyPlayerHost", "ReceiveDisconnect")]
    internal partial RpcTask Disconnect();
}