using Helios.Database.Attributes;

namespace Helios.Database.Tables.Party;

[Entity("pings")]
public class Pings : BaseTable
{
    [Column("sentBy")]
    public string SentBy { get; set; }
    [Column("meta")]
    public string Meta { get; set; }
    [Column("sentTo")]
    public string SentTo { get; set; }
    [Column("sentAt")]
    public string SentAt { get; set; }
    [Column("expiresAt")]
    public string ExpiresAt { get; set; }
}