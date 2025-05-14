using Helios.Database.Attributes;

namespace Helios.Database.Tables.Party;

[Entity("invites")]
public class Invites : BaseTable
{
    [Column("partyId")]
    public string PartyId { get; set; }
    [Column("sentBy")]
    public string SentBy { get; set; }
    [Column("meta")]
    public string Meta { get; set; }
    [Column("sentTo")]
    public string SentTo { get; set; }
    [Column("sentAt")]
    public string SentAt { get; set; }
    [Column("updatedAt")]
    public string UpdatedAt { get; set; }
    [Column("expiresAt")]
    public string ExpiresAt { get; set; }
    [Column("status")]
    public string Status { get; set; }
}