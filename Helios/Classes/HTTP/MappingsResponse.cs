using Newtonsoft.Json;

namespace Helios.Classes.HTTP;

public class MappingsResponse
{
    [JsonProperty("url")]
    public string Url { get; set; }
    [JsonProperty("fileName")]
    public string FileName { get; set; }
    [JsonProperty("hash")]
    public string Hash { get; set; }
    [JsonProperty("length")]
    public int Length { get; set; }
    [JsonProperty("uploaded")]
    public DateTime Uploaded { get; set; }
    [JsonProperty("meta")]
    public MappingsMeta Meta { get; set; }
}