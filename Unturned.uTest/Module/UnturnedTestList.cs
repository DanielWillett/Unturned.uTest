using Newtonsoft.Json;

namespace uTest.Module;


#nullable disable

internal class UnturnedTestList
{
    [JsonRequired]
    public string SessionUid { get; set; }

    [JsonRequired]
    public List<UnturnedTestReference> Tests { get; set; }

    [JsonRequired]
    public string TestListTypeName { get; set; }

    /// <summary>
    /// Full name of the test assembly.
    /// </summary>
    public string TestAssembly { get; set; }

    public bool IsAllTests { get; set; }

    /// <summary>
    /// Currently unused by uTest but may be used in the future.
    /// </summary>
    public string TreeNodeFilter { get; set; }
    /// <summary>
    /// The map to use, or <see langword="null"/> if it doesn't matter.
    /// </summary>
    public string Map { get; set; }
    public bool CollectTrxProperties { get; set; }
    public SteamIdGenerationStyle SteamIdGenerationStyle { get; set; }
    public string ClientInstallDir { get; set; }
}

internal class UnturnedTestReference
{
    [JsonRequired]
    public string Uid { get; set; }
}

/// <summary>
/// Used to determine how steam IDs are generated.
/// <see cref="DevUniverse"/> is ideal but some plugins may not be compatible. Any plugins which rely on either the Steam ID starting with 765 or being exactly 17 characters long will not be compatible with <see cref="DevUniverse"/>.
/// </summary>
public enum SteamIdGenerationStyle
{
    /// <summary>
    /// Creates SteamIDs with a very large random account number.
    /// </summary>
    /// <remarks>
    /// This ensures all steam IDs will be exactly 17 characters long,
    /// be of the 'Individual' account type,
    /// and will always begin with the digits '765' which some developers use to verify a SteamID belongs to a player.
    /// <para>
    /// It's possible for a real user account to have the same ID as a generated one, although very unlikely.
    /// </para>
    /// <para>
    /// All generated Steam IDs end in '0418'.
    /// </para>
    /// </remarks>
    Random,

    /// <summary>
    /// Creates SteamIDs in the 'Dev' universe.
    /// </summary>
    /// <remarks>
    /// This is the least compatible mode, which only ensures all steam IDs are of the 'Individual' account type
    /// and that they will not be assigned to a player.
    /// <para>
    /// All IDs will be of length 18, unlike normal individual IDs which are all 17.
    /// They will also all begin with the digits '292' instead of the usual '765'.
    /// </para>
    /// <para>
    /// All generated Steam IDs end in '0418'.
    /// </para>
    /// </remarks>
    DevUniverse
}