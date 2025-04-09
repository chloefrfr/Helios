using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Profiles;

[Entity("profiles")]
public class Profiles : BaseTable
{
    [Column("accountId")]
    [Required]
    public string AccountId { get; set; }
    [Column("profileId")]
    [Required]
    public string ProfileId { get; set; }
    [Column("revision")]
    [Required]
    public int Revision { get; set; }
}