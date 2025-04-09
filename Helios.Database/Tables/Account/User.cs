using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Account
{
    [Entity("users")]
    public class User : BaseTable
    {
        [Column("username")]
        public string Username { get; set; }
        [Required]
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
        [Column("all_items_granted")]
        [Required]
        public bool AllItemsGranted { get; set; }
        [Column("lastLogin")]
        [Required]
        public string LastLogin { get; set; }
    }
}
