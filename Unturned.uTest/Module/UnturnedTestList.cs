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

    public bool IsAllTests { get; set; }
    public bool CollectTrxProperties { get; set; }
}

internal class UnturnedTestReference
{
    [JsonRequired]
    public string Uid { get; set; }
}