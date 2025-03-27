using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Account
{
    [Entity("users")]
    public class User : BaseTable
    {
        [Key] 
        [Column("id")] 
        public int Id { get; set; }
        [Column("email")] 
        [EmailAddress]
        public string Email { get; set; }
        [Column("password")]
        [DataType(DataType.Password)] 
        public string Password { get; set; }
        [Column("accountId")]
        [Required]
        public string AccountId { get; set; }
        [Column("discordId")]
        [Required]
        public string DiscordId { get; set; }
        [Column("banned")]
        [Required]
        public bool Banned { get; set; }
    }
}
