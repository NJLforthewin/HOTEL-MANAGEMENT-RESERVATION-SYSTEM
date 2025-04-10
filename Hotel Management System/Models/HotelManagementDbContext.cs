using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_System.Models
{
    public class HotelManagementDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<HousekeepingStaff> HousekeepingStaff { get; set; }
        public DbSet<HousekeepingAssignment> HousekeepingAssignments { get; set; }

        public HotelManagementDbContext(DbContextOptions<HotelManagementDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the Booking-User relationship
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .IsRequired(false);

            // Configure HousekeepingStaff with explicit primary key
            modelBuilder.Entity<HousekeepingStaff>()
                .HasKey(s => s.StaffId);

            // Configure HousekeepingAssignment with explicit primary key
            modelBuilder.Entity<HousekeepingAssignment>()
                .HasKey(a => a.AssignmentId);

            // Configure relationships for HousekeepingAssignment
            modelBuilder.Entity<HousekeepingAssignment>()
                .HasOne(a => a.Room)
                .WithMany()
                .HasForeignKey(a => a.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HousekeepingAssignment>()
                .HasOne(a => a.Staff)
                .WithMany(s => s.Assignments)
                .HasForeignKey(a => a.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}