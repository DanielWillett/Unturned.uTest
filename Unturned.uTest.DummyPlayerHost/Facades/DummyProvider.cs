using DanielWillett.ModularRpcs.Annotations;
using DanielWillett.ModularRpcs.Async;
using SDG.Provider;
using SDG.Provider.Services;
using SDG.Provider.Services.Achievements;
using SDG.Provider.Services.Browser;
using SDG.Provider.Services.Cloud;
using SDG.Provider.Services.Community;
using SDG.Provider.Services.Multiplayer;
using SDG.Provider.Services.Statistics;
using SDG.Provider.Services.Statistics.Global;
using SDG.Provider.Services.Statistics.User;
using SDG.Provider.Services.Store;
using SDG.Provider.Services.Translation;
using SDG.SteamworksProvider.Services.Community;
using SDG.SteamworksProvider.Services.Store;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;

namespace uTest.Dummies.Host.Facades;

internal class DummyProvider(DummyPlayerHost module) : IProvider
{
    public IAchievementsService achievementsService { get; } = new DummyAchievementService();
    public IBrowserService browserService { get; } = new DummyBrowserService();
    public ICloudService cloudService { get; } = new DummyCloudService(module);
    public ICommunityService communityService { get; } = Provider.provider.communityService;
    public TempSteamworksEconomy economyService { get; } = Provider.provider.economyService;
    public TempSteamworksMatchmaking matchmakingService { get; } = Provider.provider.matchmakingService;
    public IMultiplayerService multiplayerService { get; } = Provider.provider.multiplayerService;
    public IStatisticsService statisticsService { get; } = new DummyStatisticsService(module.SteamId);
    public IStoreService storeService { get; } = new DummyStoreService();
    public ITranslationService translationService { get; } = new DummyTranslationService();
    public TempSteamworksWorkshop workshopService { get; } = Provider.provider.workshopService;

    internal void ConfigureServices(ServiceContainer cont)
    {
        cont.AddService(typeof(IProvider), this);
        cont.AddService(typeof(DummyProvider), this);

        cont.AddService(typeof(IAchievementsService), achievementsService);
        cont.AddService(typeof(DummyAchievementService), achievementsService);

        cont.AddService(typeof(IBrowserService), browserService);
        cont.AddService(typeof(DummyBrowserService), browserService);

        cont.AddService(typeof(ICloudService), cloudService);
        cont.AddService(typeof(DummyCloudService), cloudService);

        cont.AddService(typeof(ICommunityService), communityService);

        cont.AddService(typeof(TempSteamworksEconomy), economyService);

        cont.AddService(typeof(TempSteamworksMatchmaking), matchmakingService);

        cont.AddService(typeof(IMultiplayerService), multiplayerService);

        cont.AddService(typeof(IStatisticsService), statisticsService);
        cont.AddService(typeof(DummyStatisticsService), statisticsService);

        cont.AddService(typeof(IStoreService), storeService);
        cont.AddService(typeof(DummyStoreService), storeService);

        cont.AddService(typeof(ITranslationService), translationService);
        cont.AddService(typeof(DummyTranslationService), translationService);

        cont.AddService(typeof(TempSteamworksWorkshop), workshopService);
    }

    public void intialize()
    {
        achievementsService.initialize();
        browserService.initialize();
        cloudService.initialize();
        communityService.initialize();
        //economyService.initialize();
        //matchmakingService.initialize();
        multiplayerService.initialize();
        statisticsService.initialize();
        storeService.initialize();
        translationService.initialize();
        //workshopService.initialize();
    }

    public void update()
    {
        achievementsService.update();
        browserService.update();
        cloudService.update();
        communityService.update();
        economyService.updateInventory();
        multiplayerService.update();
        statisticsService.update();
        storeService.update();
        translationService.update();
        workshopService.update();
    }

    public void shutdown()
    {
        achievementsService.shutdown();
        browserService.shutdown();
        cloudService.shutdown();
        communityService.shutdown();
        //economyService.shutdown();
        //matchmakingService.shutdown();
        multiplayerService.shutdown();
        statisticsService.shutdown();
        storeService.shutdown();
        translationService.shutdown();
        //workshopService.shutdown();
    }

    public static void ShutdownOldProviderServices(IProvider other)
    {
        other.achievementsService.shutdown();
        other.browserService.shutdown();
        other.cloudService.shutdown();
        other.statisticsService.userStatisticsService.shutdown();
        other.storeService.shutdown();
        other.translationService.shutdown();
    }
}

internal class DummyAchievementService : Service, IAchievementsService
{
    private readonly HashSet<string> _achievements = new HashSet<string>();

    public bool getAchievement(string name, out bool has)
    {
        has = _achievements.Contains(name);
        return true;
    }

    public bool setAchievement(string name)
    {
        return _achievements.Add(name);
    }
}

[GenerateRpcSource]
internal partial class DummyBrowserService : Service, IBrowserService
{
    public bool canOpenBrowser => true;
    public void open(string url)
    {
        Task.Run(async () =>
        {
            try
            {
                await SendOpenBrowserWindow(url);
            }
            catch (Exception ex)
            {
                UnturnedLogLogger.Instance.LogError("Error sending browser request to server.", ex);
            }
        });
    }

    [RpcSend] // todo
    private partial RpcTask SendOpenBrowserWindow(string url);
}

internal class DummyCloudService(DummyPlayerHost module) : Service, ICloudService
{
    private string GetCloudFilePath(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (path.StartsWith("/"))
            path = path[1..];
        return Path.GetFullPath(Path.Combine(module.TemporaryDataPath, "Cloud", path));
    }

    public bool read(string path, byte[] data)
    {
        string simFilePath = GetCloudFilePath(path);
        try
        {
            using FileStream fs = new FileStream(simFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Math.Min(2048, data.Length), FileOptions.SequentialScan);
            long len = fs.Length;
            if (len > data.Length)
                return false;
            int amt = fs.Read(data, 0, data.Length);
            return amt == len;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public bool write(string path, byte[] data, int size)
    {
        string simFilePath = GetCloudFilePath(path);
        string? dirName = Path.GetDirectoryName(simFilePath);
        if (!string.IsNullOrEmpty(dirName))
            Directory.CreateDirectory(dirName);
        using FileStream fs = new FileStream(simFilePath, FileMode.Create, FileAccess.Write, FileShare.Write, Math.Min(2048, data.Length), FileOptions.SequentialScan);
        fs.Write(data, 0, size);
        return true;
    }

    public bool getSize(string path, out int size)
    {
        string simFilePath = GetCloudFilePath(path);
        try
        {
            FileInfo fInfo = new FileInfo(simFilePath);
            long s = fInfo.Length;
            size = (int)s;
            return s <= int.MaxValue;
        }
        catch (FileNotFoundException)
        {
            size = 0;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            size = 0;
            return false;
        }
    }

    public bool exists(string path, out bool exists)
    {
        exists = File.Exists(GetCloudFilePath(path));
        return true;
    }

    public bool delete(string path)
    {
        string simFilePath = GetCloudFilePath(path);
        try
        {
            if (File.Exists(simFilePath))
            {
                File.Delete(simFilePath);
                return true;
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }

        return false;
    }
}

internal class DummyStatisticsService(CSteamID steam64) : Service, IStatisticsService
{
    public IUserStatisticsService userStatisticsService { get; } = new DummyUserStatisticsService(steam64);
    public IGlobalStatisticsService globalStatisticsService { get; } = Provider.provider.statisticsService.globalStatisticsService;

    public override void update()
    {
        userStatisticsService.update();
        globalStatisticsService.update();
    }

    public override void initialize()
    {
        userStatisticsService.initialize();
        globalStatisticsService.initialize();
    }

    public override void shutdown()
    {
        userStatisticsService.shutdown();
        globalStatisticsService.shutdown();
    }
}

internal class DummyUserStatisticsService : Service, IUserStatisticsService
{
    private readonly CSteamID _steam64;
    public event UserStatisticsRequestReady? onUserStatisticsRequestReady;

    private readonly Dictionary<string, KeyValuePair<int?, float?>> _statistics;

    public DummyUserStatisticsService(CSteamID steam64)
    {
        _statistics = new Dictionary<string, KeyValuePair<int?, float?>>();
        _steam64 = steam64;
    }

    public bool getStatistic(string name, out int data)
    {
        if (_statistics.TryGetValue(name, out KeyValuePair<int?, float?> kvp))
        {
            data = kvp.Key.GetValueOrDefault();
            return kvp.Key.HasValue;
        }

        data = 0;
        return false;
    }

    public bool setStatistic(string name, int data)
    {
        _statistics[name] = new KeyValuePair<int?, float?>(data, null);
        return true;
    }

    public bool getStatistic(string name, out float data)
    {
        if (_statistics.TryGetValue(name, out KeyValuePair<int?, float?> kvp))
        {
            data = kvp.Value.GetValueOrDefault();
            return kvp.Value.HasValue;
        }

        data = 0f;
        return false;
    }

    public bool setStatistic(string name, float data)
    {
        _statistics[name] = new KeyValuePair<int?, float?>(null, data);
        return true;
    }

    public bool requestStatistics()
    {
        onUserStatisticsRequestReady?.Invoke(new SteamworksCommunityEntity(_steam64));
        return true;
    }
}

internal class DummyStoreService : Service, IStoreService
{
    public void open(IStorePackageID packageID)
    {
        string appId = packageID is SteamworksStorePackageID pkgId ? pkgId.appID.m_AppId.ToString(CultureInfo.InvariantCulture) : packageID.ToString();
        Provider.provider.browserService.open("https://store.steampowered.com/app/" + appId);
    }
}

internal class DummyTranslationService : Service, ITranslationService
{
    public string language => Provider.language;
}