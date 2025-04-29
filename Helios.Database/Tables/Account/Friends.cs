using Helios.Database.Attributes;

namespace Helios.Database.Tables.Account;

[Entity("friends")]
public class Friends : BaseTable
{
    [Column("accountId")]
    public string AccountId { get; set; }
    [Column("createdAt")]
    public string CreatedAt { get; set; }
    [Column("friendId")]
    public string FriendId { get; set; }
    [Column("alias")] 
    public string Alias { get; set; }
    [Column("direction")]
    public string Direction { get; set; }
    [Column("status")]
    public string Status { get; set; }
}