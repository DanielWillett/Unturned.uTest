using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace uTest.Runner.Util;

internal static class UnityInstallationHelper
{
    private static readonly Regex ReadUnityVersionRegex = new Regex(
        @"m_EditorVersion:\s*(\S*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
    );

    internal static bool TryGetUnityVersionFromProject(string projectRootDir, out UnityEngineVersion version)
    {
        string projectVersionTxt = Path.Combine(projectRootDir, "ProjectSettings", "ProjectVersion.txt");
        string text;
        try
        {
            text = File.ReadAllText(projectVersionTxt);
        }
        catch
        {
            version = default;
            return false;
        }

        Match match = ReadUnityVersionRegex.Match(text);
        if (!match.Success || match.Groups.Count != 2)
        {
            version = default;
            return false;
        }

        string ver = match.Groups[1].Value;
        return UnityEngineVersion.TryParse(ver, out version);
    }

    internal static bool TryFindUnityInstall(
        UnityEngineVersion minVersion,
        [NotNullWhen(true)] out string? exe,
        out UnityEngineVersion version)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return WindowsUnityInstallationHelper.TryFindUnityInstall(minVersion, out exe, out version);
        }

        return UnixUnityInstallationHelper.TryFindUnityInstall(minVersion, out exe, out version);
    }
}

file static class WindowsUnityInstallationHelper
{
    public static bool TryFindUnityInstall(
        UnityEngineVersion minVersion,
        [NotNullWhen(true)] out string? exe,
        out UnityEngineVersion version)
    {
        if (TryGetFromFileExtensionKey(".unity", minVersion, out exe, out version))
        {
            return true;
        }
        if (TryGetFromFileExtensionKey(".unityPackage", minVersion, out exe, out version))
        {
            return true;
        }

        exe = null;
        version = default;
        return false;
    }

    private static bool TryGetFromFileExtensionKey(string ext, UnityEngineVersion minVersion, [NotNullWhen(true)] out string? exe, out UnityEngineVersion version)
    {
        using RegistryKey? subKey = Registry.ClassesRoot.OpenSubKey(ext);
        if (subKey == null)
        {
            exe = null;
            version = default;
            return false;
        }

        using RegistryKey? openWithProgIds = subKey.OpenSubKey("OpenWithProgids");
        if (openWithProgIds == null)
        {
            exe = null;
            version = default;
            return false;
        }

        string[] allNames = openWithProgIds.GetValueNames();

        string? lowestExe = null;
        UnityEngineVersion lowestVersion = default;

        foreach (string name in allNames)
        {
            if (!name.StartsWith("Unity.", StringComparison.Ordinal)
                || !name.EndsWith(ext, StringComparison.Ordinal)
                || name.Length <= 12)
            {
                continue;
            }

            if (!UnityEngineVersion.TryParse(name.Substring(6, name.Length - 12), out UnityEngineVersion thisVersion))
            {
                continue;
            }

            if (thisVersion < minVersion || lowestExe != null && lowestVersion < thisVersion)
            {
                continue;
            }

            using RegistryKey? classInfo = Registry.ClassesRoot.OpenSubKey(name);
            using RegistryKey? icon = classInfo?.OpenSubKey("DefaultIcon");

            exe = null;
            // try to get exe from DefaultIcon ("C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",0)
            if (icon?.GetValue(null) is string ico)
            {
                int endIndex = ico.LastIndexOf('"');
                int firstIndex = ico.IndexOf('"');
                if (endIndex != -1 && firstIndex != -1 && endIndex != firstIndex)
                {
                    ico = ico.Substring(firstIndex + 1, endIndex - firstIndex - 1);
                }

                if (Path.GetFileName(ico).Equals("Unity.exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(ico))
                {
                    exe = ico;
                }
            }

            if (exe == null)
            {
                // as a fallback, try getting it from the 'open with file' command
                RegistryKey? command = classInfo?.OpenSubKey(@"Shell\Open\Command");
                if (command?.GetValue(null) is not string { Length: > 0 } cmd)
                    continue;

                if (cmd[0] == '"' && cmd.Length > 1)
                {
                    int nextIndex = cmd.IndexOf('"', 1);
                    if (nextIndex >= 1)
                    {
                        cmd = cmd[1..nextIndex];
                    }
                }

                if (Path.GetFileName(cmd).Equals("Unity.exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(cmd))
                {
                    exe = cmd;
                }
            }

            if (exe == null)
                continue;

            lowestExe = exe;
            lowestVersion = thisVersion;
        }

        exe = lowestExe;
        version = lowestVersion;
        return lowestExe != null;
    }
}

file static class UnixUnityInstallationHelper
{
    public static bool TryFindUnityInstall(
        UnityEngineVersion minVersion,
        [NotNullWhen(true)] out string? exe,
        out UnityEngineVersion version)
    {
        string dir;
        bool osx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (osx)
        {
            dir = "/Applications/Unity/Hub/Editor";
        }
        else
        {
            dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "Unity",
                "Hub",
                "Editor"
            );
        }

        UnityEngineVersion lowestVersion = default;
        string? lowestExe = null;

        foreach (string folder in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
        {
            string fn = Path.GetFileName(folder);
            if (!UnityEngineVersion.TryParse(fn, out UnityEngineVersion v) || v < minVersion)
            {
                continue;
            }

            if (lowestExe != null && v > lowestVersion)
                continue;

            string bin = Path.Combine(folder, osx ? "Unity.app/Contents/MacOS/Unity" : "Editor/Unity");
            if (File.Exists(bin))
            {
                lowestExe = bin;
                lowestVersion = v;
            }
        }

        exe = lowestExe;
        version = lowestVersion;
        return lowestExe != null;
    }
}