using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Profiles;
 
[Entity("profile_items")]
public class Items : BaseTable
{
    [Column("accountId")]
    [Required]
    public string AccountId { get; set; }
    [Column("profileId")]
    [Required]
    public string ProfileId { get; set; }
    [Column("templateId")]
    [Required]
    public string TemplateId { get; set; }
    [Column("value")]
    [Required]
    public string Value { get; set; }
    [Column("quantity")]
    [Required]
    public int Quantity { get; set; }
}