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
/// <see cref="DevUniverse"/> is ideal but some plugins may not be compatible.
/// </summary>
public enum SteamIdGenerationStyle
{
    /// <summary>
    /// Creates SteamIDs in the 'Dev' universe.
    /// </summary>
    DevUniverse,
    
    /// <summary>
    /// Creates SteamIDs with the 'account instance' bits set to a larger number than the default (1).
    /// </summary>
    Instance,

    /// <summary>
    /// Creates SteamIDs with a very large random account number.
    /// </summary>
    Random
}