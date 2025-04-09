using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class PastSeasons
{
    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; set; }
    [JsonPropertyName("numWins")]
    public int NumWins { get; set; }
    [JsonPropertyName("numHighBracket")]
    public int NumHighBracket { get; set; }
    [JsonPropertyName("numLowBracket")]
    public int NumLowBracket { get; set; }
    [JsonPropertyName("seasonXp")]
    public int SeasonXp { get; set; }
    [JsonPropertyName("seasonLevel")]
    public int SeasonLevel { get; set; }
    [JsonPropertyName("bookXp")]
    public int BookXp { get; set; }
    [JsonPropertyName("bookLevel")]
    public int BookLevel { get; set; }
    [JsonPropertyName("purchasedVIP")]
    public bool PurchasedVIP { get; set; }
    [JsonPropertyName("numRoyalRoyales")]
    public int NumRoyalRoyales { get; set; }
    [JsonPropertyName("survivorTier")]
    public int SurvivorTier { get; set; }
    [JsonPropertyName("survivorPrestige")]
    public int SurvivorPrestige { get; set; }
}