using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Profiles;

[Entity("quests")]
public class Quests : BaseTable
{
    [Column("accountId")]
    [Required]
    public string AccountId { get; set; }
    [Column("profileId")]
    [Required]
    public string ProfileId { get; set; } // If i implement stw in the future
    [Column("templateId")]
    [Required]
    public string TemplateId { get; set; }
    [Column("value")]
    [Required]
    public string Value { get; set; }
    [Column("isDaily")]
    [Required]
    public bool IsDaily { get; set; }
    [Required]
    [Column("season")]
    public int Season { get; set; }
}