using Newtonsoft.Json;

namespace uTest.Module;


#nullable disable

internal class UnturnedTestList
{
    [JsonRequired]
    public string SessionUid { get; set; }

    [JsonRequired]
    public List<UnturnedTestReference> Tests { get; set; }
}

internal class UnturnedTestReference
{
    public string TypeName { get; set; }

    public string MethodName { get; set; }

    public string[] ParameterTypeNames { get; set; }

    [JsonRequired]
    public string Uid { get; set; }

    public int MetadataToken { get; set; }
}