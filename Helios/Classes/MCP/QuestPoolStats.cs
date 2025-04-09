using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class QuestPoolStats
{
    [JsonPropertyName("dailyLoginInterval")]
    public string DailyLoginInterval { get; set; }
    [JsonPropertyName("poolLockouts")]
    public PoolLockouts PoolLockouts { get; set; }
    [JsonPropertyName("poolStats")]
    public List<PoolStat> PoolStats { get; set; }
}