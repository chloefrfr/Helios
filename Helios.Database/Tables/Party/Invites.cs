using Helios.Database.Attributes;

namespace Helios.Database.Tables.Party;

[Entity("invites")]
public class Invites : BaseTable
{
    [Column("partyId")]
    public string PartyId { get; set; }
    [Column("sent_by")]
    public string SentBy { get; set; }
    [Column("meta")]
    public string Meta { get; set; }
    [Column("sent_to")]
    public string SentTo { get; set; }
    [Column("sent_at")]
    public string SentAt { get; set; }
    [Column("updated_at")]
    public string UpdatedAt { get; set; }
    [Column("expires_at")]
    public string ExpiresAt { get; set; }
    [Column("status")]
    public string Status { get; set; }
}