using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_System.Models
{
    public class HotelManagementDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public HotelManagementDbContext(DbContextOptions<HotelManagementDbContext> options)
            : base(options)
        {
        }
    }
}
