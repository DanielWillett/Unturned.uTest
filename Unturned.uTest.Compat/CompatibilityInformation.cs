using System;

namespace uTest.Compat;

/// <summary>
/// Information about which compatible modules are detected. Multiple modules can be installed at once.
/// </summary>
public static class CompatibilityInformation
{
    // initialized in MainModuleLoader.initialize

    // NOTE: Contact @danielwillett on Discord for compatibility inquiries.

    internal static (string ModuleName, Action<bool> Setter)[] CompatibleModules =
    [
        ("OpenMod.Unturned",    installed => IsOpenModInstalled     = installed),
        ("Rocket.Unturned",     installed => IsRocketInstalled      = installed),
        ("Uncreated.Warfare",   installed => IsUncreatedInstalled   = installed)
    ];

    /// <summary>
    /// Whether or not the <see href="https://github.com/DanielWillett/Unturned.uTest">uTest</see> unit testing module is installed.
    /// </summary>
    /// <remarks>Can be used to see if the current environment is a test environment.</remarks>
    public static bool IsUnturnedTestInstalled { get; internal set; }

    /// <summary>
    /// Whether or not the <see href="https://github.com/SmartlyDressedGames/Legally-Distinct-Missile">Legally Distinct Missile</see> (RocketMod) plugin framework is installed.
    /// </summary>
    /// <remarks>Only initialized if <see cref="IsUnturnedTestInstalled"/> is <see langword="true"/>.</remarks>
    public static bool IsRocketInstalled { get; private set; }

    /// <summary>
    /// Whether or not the <see href="https://openmod.github.io/openmod-docs">OpenMod</see> plugin framework is installed.
    /// </summary>
    /// <remarks>Only initialized if <see cref="IsUnturnedTestInstalled"/> is <see langword="true"/>.</remarks>
    public static bool IsOpenModInstalled { get; private set; }

    /// <summary>
    /// Whether or not the <see href="https://github.com/UncreatedStaff/UncreatedWarfare">Uncreated</see> total conversion framework is installed.
    /// </summary>
    /// <remarks>Only initialized if <see cref="IsUnturnedTestInstalled"/> is <see langword="true"/>.</remarks>
    public static bool IsUncreatedInstalled { get; private set; }
}