using Newtonsoft.Json;

namespace Helios.Classes.HTTP;

public class MappingsMeta
{
    [JsonProperty("version")]
    public string Version { get; set; }
    [JsonProperty("compressionMethod")]
    public string CompressionMethod { get; set; }
    [JsonProperty("platform")]
    public string Platform { get; set; }
}