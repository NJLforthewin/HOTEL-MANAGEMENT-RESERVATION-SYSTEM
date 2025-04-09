using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public class HousekeepingAssignment
    {
        [Key]
        public int AssignmentId { get; set; }

        public int RoomId { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; } = null!;

        public int StaffId { get; set; }

        [ForeignKey("StaffId")]
        public virtual HousekeepingStaff Staff { get; set; } = null!;

        [Required]
        public string Priority { get; set; } = "Normal";

        public string Notes { get; set; } = string.Empty;

        [Display(Name = "Assigned At")]
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        [Display(Name = "Completed At")]
        public DateTime? CompletedAt { get; set; }

        [Required]
        public string Status { get; set; } = "Assigned";
    }
}