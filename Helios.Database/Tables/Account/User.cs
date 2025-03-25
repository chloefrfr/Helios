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

        [Column("username")] 
        public string Username { get; set; }

        [Column("email")]
        [EmailAddress]
        public string Email { get; set; }

        [Column("password")]
        [DataType(DataType.Password)] 
        public string Password { get; set; }
    }
}
