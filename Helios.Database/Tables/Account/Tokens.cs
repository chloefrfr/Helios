using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Account;

[Entity("tokens")]
public class Tokens : BaseTable
{
    [Column("accountId")]
    [Required]
    public string AccountId { get; set; }
    
    [Column("type")]
    [Required]
    public string Type { get; set; }

    [Column("token")]
    [Required]
    [Key]
    public string Token { get; set; }
}