using System;

namespace Hotel_Management_System.Models
{
    public class User
    {
        public int UserId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }  
        public DateTime CreatedAt { get; set; }
        public required string Role { get; set; }
    }
}
