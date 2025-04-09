using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class Variants
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; }
    [JsonPropertyName("active")]
    public string Active { get; set; }
    [JsonPropertyName("owned")]
    public List<string> Owned { get; set; }
}