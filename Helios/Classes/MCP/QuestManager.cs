using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class QuestManager
{
    [JsonPropertyName("dailyLoginInterval")]
    public string DailyLoginInterval { get; set; }
    [JsonPropertyName("dailyQuestRerolls")]
    public int DailyQuestRerolls { get; set; }
    [JsonPropertyName("questPoolStats")]
    public QuestPoolStats QuestPoolStats { get; set; }
}