using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        public int Capacity { get; set; }

        [Required]
        public required string RoomNumber { get; set; }

        [Required]
        public required string Category { get; set; }

        [Required]
        public required string Status { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerNight { get; set; }

        public DateTime? LastCleaned { get; set; }
        public required string MaintenanceNotes { get; set; }


        // Navigation Property (One Room has many Bookings)
        public virtual List<Booking>? Bookings { get; set; }
    }
}
