using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables;

public abstract class BaseTable
{
    [PrimaryKey]
    [Key]
    [Column("id")]
    public int Id { get; set; }
}