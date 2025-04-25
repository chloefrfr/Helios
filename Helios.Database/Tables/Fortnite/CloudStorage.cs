using Helios.Database.Attributes;

namespace Helios.Database.Tables.Fortnite;

[Entity("cloudstorage")]
public class CloudStorage : BaseTable
{
    [Column("filename")]
    public string Filename { get; set; }
    [Column("value")]
    public string Value { get; set; }
    [Column("enabled")]
    public bool Enabled { get; set; }
}