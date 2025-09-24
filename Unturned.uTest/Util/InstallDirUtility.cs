using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace uTest;

/// <summary>
/// Utility to find the install directory for Steam games.
/// </summary>
public class InstallDirUtility
{
    private readonly bool _u3ds;
    private readonly ILogger _logger;
    private readonly string _cacheFile;

    private string GameId => _u3ds ? "1110390" : "304930";
    private string GameName => _u3ds ? "U3DS" : "Unturned";

    public string GetCache()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "uTest"
            );
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "Library/Cache/uTest"
            );
        }

        return "/var/cache/uTest";
    }

    /// <summary>
    /// Picks out the install directory from the library file as the last match
    /// </summary>
    private readonly Regex _libraryVcfFindPathRegex;

    private string? _installDirectory;

    /// <summary>
    /// Set an explicit directory to use instead of automatically finding it.
    /// </summary>
    public string? OverrideInstallDirectory
    {
        get;
        set
        {
            field = value;
            _installDirectory = OverrideInstallDirectory;
        }
    }

    /// <summary>
    /// The cached install directory, only available after the first time <see cref="TryGetInstallDirectory"/> is ran.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">Could not locate the game's installation directory.</exception>
    public string InstallDirectory
    {
        get
        {
            if (_installDirectory == null && !TryGetInstallDirectory(out _installDirectory))
            {
                throw new DirectoryNotFoundException($"Failed to locate the {GameName} ({GameId}) installation directory.");
            }

            return _installDirectory;
        }
    }

    public InstallDirUtility(bool u3ds, ILogger logger)
    {
        _u3ds = u3ds;
        _logger = logger;
        _libraryVcfFindPathRegex = new Regex(
            $"""\"path\"\s*\"([^\n\r]*)\"(?=.*\"{GameId}\")""",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled
        );

        _cacheFile = Path.Combine(GetCache(), u3ds ? "prev_u3ds_location.txt" : "prev_unturned_location.txt");

        try
        {
            if (!File.Exists(_cacheFile))
                return;

            string location = File.ReadAllText(_cacheFile);
            if (!string.IsNullOrWhiteSpace(location)
                && (File.Exists(Path.Combine(location, "Unturned_Data", "Managed", "Assembly-CSharp.dll"))
                    || File.Exists(Path.Combine(location, "Unturned_Headless_Data", "Managed", "Assembly-CSharp.dll"))))
            {
                _installDirectory = location;
            }
        }
        catch (SystemException ex)
        {
            logger.LogError("Unable to read cache file.", ex);
        }
    }

    /// <summary>
    /// Marks the install directory as possibly changed and requires it be re-fetched next time it's needed.
    /// </summary>
    public void InvalidateInstallDirectory()
    {
        _installDirectory = null;
    }

    /// <summary>
    /// Attempts to automatically locate the installation location of the game and workshop folders.
    /// </summary>
    public bool TryGetInstallDirectory(out string installDir)
    {
        if (_installDirectory != null)
        {
            installDir = _installDirectory;
            return true;
        }

        if (_u3ds)
        {
            string? dir = FindUnturnedInstallation(_logger);
            if (dir != null)
            {
                installDir = dir;
                _installDirectory = dir;
                WriteCache(dir);
                return true;
            }
        }

        installDir = null!;
        string libraryFilePath;
        bool isUnix = false;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!WindowsInstallDirUtility.TryFindSteamInstallDirectory(out libraryFilePath, _logger))
            {
                return false;
            }
        }
        else if (!UnixInstallDirUtility.TryFindSteamInstallDirectory(out libraryFilePath, _logger))
        {
            return false;
        }
        else
        {
            isUnix = true;
        }

        if (!TryFindGame(isUnix, libraryFilePath, out string unturnedPath))
        {
            return false;
        }

        _installDirectory = unturnedPath;
        WriteCache(unturnedPath);
        installDir = unturnedPath;
        return true;
    }

    private void WriteCache(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_cacheFile);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_cacheFile, path);
        }
        catch (SystemException ex)
        {
            _logger.LogError("Unable to write cache file.", ex);
        }
    }

    private bool TryFindGame(bool isUnix, string libraryFilePath, out string unturnedPath)
    {
        MatchCollection matches = _libraryVcfFindPathRegex.Matches(File.ReadAllText(libraryFilePath));
        unturnedPath = null!;
        if (matches.Count == 0 || matches[matches.Count - 1].Groups.Count <= 1)
        {
            _logger.LogError($"Failed to match {GameName} installation in: \"{libraryFilePath}\".");
            return false;
        }

        string libraryDir = matches[matches.Count - 1].Groups[1].Value;
        if (!isUnix)
        {
            libraryDir = libraryDir.Replace(@"\\", @"\");
        }
        if (!Directory.Exists(libraryDir))
        {
            _logger.LogError($"Library \"{libraryDir}\" has been removed.");
            return false;
        }

        string gameInstallDir = Path.Combine(libraryDir, "steamapps", "common", GameName);
        if (!Directory.Exists(gameInstallDir))
        {
            if (!isUnix)
            {
                _logger.LogError($"{GameName} installation at \"{gameInstallDir}\" has been removed.");
                return false;
            }

            gameInstallDir = Path.Combine(libraryDir, "SteamApps", "common", GameName);
            if (!Directory.Exists(gameInstallDir))
            {
                _logger.LogError($"{GameName} installation at \"{gameInstallDir}\" has been removed.");
                return false;
            }
        }

        unturnedPath = gameInstallDir;
        return true;
    }

    internal string? FindUnturnedInstallation(ILogger logger)
    {
        string[] pathsToSearch;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string systemRoot = Path.GetPathRoot(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows));
            List<DriveInfo> fixedDrives;
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();

                // all fixed drives besides the system drive that are ready
                fixedDrives = drives
                    .Where(x => x.DriveType == DriveType.Fixed
                                && x.IsReady
                                && !string.Equals(x.RootDirectory.FullName, systemRoot, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                logger.LogWarning("Failed to access drive information, not checking secondary drives.");
                fixedDrives = new List<DriveInfo>(0);
            }

            pathsToSearch = new string[fixedDrives.Count * 3 + 6];

            pathsToSearch[0] = Path.Combine(systemRoot, "SteamCMD");
            pathsToSearch[1] = Path.Combine(systemRoot, "Unturned");
            pathsToSearch[2] = Path.Combine(systemRoot, "U3DS");
            pathsToSearch[3] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            pathsToSearch[4] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            pathsToSearch[5] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
            for (int i = 0; i < fixedDrives.Count; ++i)
            {
                string root = fixedDrives[i].RootDirectory.FullName;
                pathsToSearch[i * 3 + 6] = Path.Combine(root, "SteamCMD");
                pathsToSearch[i * 3 + 7] = Path.Combine(root, "Unturned");
                pathsToSearch[i * 3 + 8] = Path.Combine(root, "U3DS");
            }
        }
        else
        {
            pathsToSearch = [ System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) ];
        }

        foreach (string path in pathsToSearch)
        {
            if (!Directory.Exists(path))
                continue;

            // Assembly-CSharp.dll will be present in other games too, this is better.
            string? dll = RecursiveFindFiles(path);
            if (dll != null)
                return dll;
        }

        return null;
    }

    private string? RecursiveFindFiles(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "SDG.NetPak.Runtime.dll", SearchOption.TopDirectoryOnly))
            {
                // determines the root folder from SDG.NetPak.Runtime.dll
                string? dir = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(dir) || !string.Equals(Path.GetFileName(dir), "Managed"))
                    continue;

                dir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir))
                    continue;

                StringComparison comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

                string dataFolder = Path.GetFileName(dir);
                if (!(string.Equals(dataFolder, "Unturned_Data", comparer)
                      || string.Equals(dataFolder, "Unturned_Headless_Data", comparer)
                      || isMacOS && string.Equals(dataFolder, "Data", comparer)))
                    continue;

                // Unturned_Data/.. -> Root (unless mac)
                dir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir))
                    continue;

                string beClient;
                if (isMacOS)
                {
                    if (!string.Equals(Path.GetFileName(dir), "Resources", comparer))
                        continue;

                    // Resources/.. -> Contents
                    dir = Path.GetDirectoryName(dir);
                    if (string.IsNullOrEmpty(dir) || !string.Equals(Path.GetFileName(dir), "Contents", comparer))
                        continue;

                    // Contents/.. -> Unturned.app
                    dir = Path.GetDirectoryName(dir);
                    if (string.IsNullOrEmpty(dir))
                        continue;

                    // Unturned.app/../BattlEye/BEClient_x64.dylib
                    beClient = Path.Combine(dir, "..", "BattlEye", "BEClient_x64.dylib");
                }
                else
                {
                    string beClientName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? IntPtr.Size == 4 ? "BEClient.dll" : "BEClient_x64.dll"
                        : "BEClient_x64.so";

                    beClient = Path.Combine(dir, "BattlEye", beClientName);
                }

                // if the BEClient library is there then it's a client build and skip it
                if (File.Exists(beClient) == _u3ds)
                    continue;

                return dir;
            }

            foreach (string folder in Directory.EnumerateDirectories(path))
            {
                string? str = RecursiveFindFiles(folder);
                if (str != null)
                    return str;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}

file static class UnixInstallDirUtility
{
    private static string[]? _linuxInstallPaths;

    // ReSharper disable once EmptyConstructor (beforefieldinit)
    static UnixInstallDirUtility() { }

    private static void CheckLinuxInstallPaths()
    {
        // steam seems to install in various paths on Linux depending on how it was installed
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        _linuxInstallPaths =
        [
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".steam"),
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, "Steam"),
            Path.Combine(home, "snap/steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/.steam")
        ];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryFindSteamInstallDirectory(out string libraryVcf, ILogger logger)
    {
        libraryVcf = null!;

        // MacOS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            const string defaultLocation = "Library/Application Support/Steam";

            string steamDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                defaultLocation
            );

            return CheckUnixSteamDir(steamDir, ref libraryVcf, logger, true);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogError($"Platform not supported: {RuntimeInformation.OSDescription}.");
            return false;
        }

        // Linux
        if (_linuxInstallPaths == null)
        {
            CheckLinuxInstallPaths();
        }

        foreach (string dir in _linuxInstallPaths!)
        {
            if (CheckUnixSteamDir(dir, ref libraryVcf, logger, false))
            {
                return true;
            }
        }

        logger.LogError($"Steam directory not found in any of the following paths: \"{string.Join("\", \"", _linuxInstallPaths)}\". Automatic discovery is not supported on Linux, consider manually changing the install directory.");
        return false;
    }

    private static bool CheckUnixSteamDir(string steamDir, ref string libraryVcf, ILogger logger, bool logDirNotFound)
    {
        if (!Directory.Exists(steamDir))
        {
            if (logDirNotFound)
                logger.LogError($"Steam directory not found in \"{steamDir}\". Automatic discovery is not supported on MacOS, consider manually changing the install directory.");
            return false;
        }

        string libraryFilePath = steamDir + "/steamapps/libraryfolders.vdf";

        if (!File.Exists(libraryFilePath))
        {
            libraryFilePath = steamDir + "/SteamApps/libraryfolders.vdf";
            if (!File.Exists(libraryFilePath))
            {
                logger.LogError($"Failed to recognize Steam directory: \"{steamDir}\" because the library configuration file at \"{libraryFilePath}\" was missing.");
                return false;
            }
        }

        libraryVcf = libraryFilePath;
        return true;
    }
}

file static class WindowsInstallDirUtility
{
    // ReSharper disable once EmptyConstructor (beforefieldinit)
    static WindowsInstallDirUtility() { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryFindSteamInstallDirectory(out string libraryVcf, ILogger logger)
    {
        libraryVcf = null!;

        string? steamDir;
        try
        {
            steamDir = (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)
                        ?? Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null))
                ?.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to access the registry.", ex);
            steamDir = null;
        }

        if (steamDir == null)
        {
            string defaultDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam");
            steamDir = defaultDir;
            logger.LogWarning($"Failed to find Steam directory in registry, falling back to {defaultDir}.");
        }

        if (!Directory.Exists(steamDir))
        {
            logger.LogError($"Steam directory \"{steamDir}\" was removed.");
            return false;
        }

        string libraryFilePath = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFilePath))
        {
            logger.LogError($"Failed to recognize Steam directory: \"{steamDir}\" because the library configuration file at \"{libraryFilePath}\" was missing.");
            return false;
        }

        libraryVcf = libraryFilePath;
        return true;
    }
}