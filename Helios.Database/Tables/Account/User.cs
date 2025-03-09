using System.ComponentModel.DataAnnotations;
using Helios.Database.Attributes;

namespace Helios.Database.Tables.Account
{
    [Entity("users")] // Maps the User class to the "users" table
    public class User : BaseTable
    {
        [Key] // Marks this property as the primary key
        [Column("id")] // Maps to the "id" column in the database
        public int Id { get; set; }

        [Column("username")] // Maps to the "username" column
        public string Username { get; set; }

        [Column("email")] // Maps to the "email" column
        [EmailAddress] // Adds validation for email format
        public string Email { get; set; }

        [Column("password")] // Maps to the "password" column
        [DataType(DataType.Password)] // Indicates this is a password field
        public string Password { get; set; }
    }
}