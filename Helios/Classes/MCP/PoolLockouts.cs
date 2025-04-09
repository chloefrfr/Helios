using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class PoolLockouts
{
    [JsonPropertyName("poolLockoutsList")]
    public List<PoolLockout> PoolLockoutsList { get; set; }
}