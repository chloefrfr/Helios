using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class PoolStat
{
    [JsonPropertyName("questHistory")]
    public List<string> QuestHistory { get; set; }
    [JsonPropertyName("rerollsRemaining")]
    public int RerollsRemaining { get; set; }
    [JsonPropertyName("nextRefresh")]
    public string NextRefresh { get; set; }
    [JsonPropertyName("poolName")]
    public string PoolName { get; set; }
}