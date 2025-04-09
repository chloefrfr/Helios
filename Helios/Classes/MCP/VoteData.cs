using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class VoteData
{
    [JsonPropertyName("electionId")]
    public string ElectionId { get; set; }
    [JsonPropertyName("voteHistory")]
    public Dictionary<string, object> VoteHistory { get; set; }
    [JsonPropertyName("votesRemaining")]
    public int VotesRemaining { get; set; }
    [JsonPropertyName("lastVoteGranted")]
    public string LastVoteGranted { get; set; }
}