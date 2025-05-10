using Helios.Database.Attributes;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Database.Tables.Fortnite;

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
    [JsonProperty("captain")]
    public string? Captain { get; set; }
    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; }
    [JsonProperty("joined_at")]
    public string JoinedAt { get; set; }
    [JsonProperty("jid")]
    public string? Jid { get; set; }
}

public class PartyMemberConnection
{
    [JsonProperty("id")]
    public string Id { get; set; }
    [JsonProperty("connected_at")]
    public string ConnectedAt { get; set; }
    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; }
    [JsonProperty("yield_leadership")]
    public bool YieldLeadership { get; set; }
    [JsonProperty("meta")]
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
    [Column("party_id")]
    public string PartyId { get; set; }
    [Column("created_at")]
    public string CreatedAt { get; set; }
    [Column("updated_at")]
    public string UpdatedAt { get; set; }
    [Column("config")]
    public string Config { get; set; } 
    [Column("members")]
    public string Members { get; set; } = JsonSerializer.Serialize(new PartyMember());
    [Column("applicants")]
    public string[] Applicants { get; set; } = Array.Empty<string>();   
    [Column("meta")]
    public Dictionary<string, string> Meta { get; set; }
    [Column("invites")]
    public string Invites { get; set; } = JsonSerializer.Serialize(new PartyInvite());
    [Column("revision")] 
    public int Revision { get; set; } = 0;
}