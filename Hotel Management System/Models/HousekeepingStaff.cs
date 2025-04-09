using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_System.Models
{
    public class HousekeepingStaff
    {
        public HousekeepingStaff()
        {
            // Initialize the collection in the constructor
            Assignments = new List<HousekeepingAssignment>();
        }

        [Key]
        public int StaffId { get; set; }

        // Added for the view links
        public int? UserId { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Position { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual ICollection<HousekeepingAssignment> Assignments { get; set; }
    }
}