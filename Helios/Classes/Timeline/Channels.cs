using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Helios.Utilities.Timeline
{
    public class Channels
    {
        [JsonPropertyName("client-matchmaking")]
        public ClientMatchmaking clientMatchmaking { get; set; }
        [JsonPropertyName("community-votes")]
        public CommunityVotes communityVotes { get; set; }
        [JsonPropertyName("client-events")]
        public ClientEvents clientEvents { get; set; }
    }
}
