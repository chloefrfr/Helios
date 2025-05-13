using System.Text.Json.Serialization;
using Helios.Database.Attributes;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Database.Tables.Party;

public class PartyMember
{
    [JsonProperty("account_id")]
    public string AccountId { get; set; }
    [JsonProperty("role")]
    public string Role { get; set; }
    [JsonProperty("meta")]
    public Dictionary<string, dynamic> Meta { get; set; }
    [JsonProperty("connections")]
    public string Connections { get; set; } = JsonSerializer.Serialize(new PartyMemberConnection());
    [JsonProperty("revision")]
    public int Revision { get; set; }
    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; }
    [JsonProperty("joined_at")]
    public string JoinedAt { get; set; }
}

public class PartyMemberConnection
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("connected_at")]
    public string ConnectedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; }
    [JsonPropertyName("yield_leadership")]
    public bool YieldLeadership { get; set; }
    [JsonPropertyName("meta")]
    public Dictionary<string, dynamic> Meta { get; set; }
}

public class PartyInvite
{
    [JsonProperty("party_id")]
    public string PartyId { get; set; }
    [JsonProperty("sent_by")]
    public string SentBy { get; set; }
    [JsonProperty("meta")]
    public dynamic Meta { get; set; }
    [JsonProperty("sent_to")]
    public string SentTo { get; set; }
    [JsonProperty("sent_at")]
    public string SentAt { get; set; }
    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; }
    [JsonProperty("expires_at")]
    public string ExpiresAt { get; set; }
    [JsonProperty("status")]
    public string Status { get; set; }
}

[Entity("parties")]
public class Parties : BaseTable
{
    [Column("partyId")]
    public string PartyId { get; set; }
    [Column("createdAt")]
    public string CreatedAt { get; set; }
    [Column("updatedAt")]
    public string UpdatedAt { get; set; }
    [Column("config")]
    public string Config { get; set; } 
    [Column("members")]
    public string Members { get; set; } = JsonSerializer.Serialize(new PartyMember());
    [Column("applicants")]
    public string[] Applicants { get; set; } = Array.Empty<string>();   
    [Column("meta")]
    public string Meta { get; set; }
    [Column("revision")] 
    public int Revision { get; set; } = 0;
    [Column("intentions")]
    public string[] Intentions { get; set; } = Array.Empty<string>();   
}