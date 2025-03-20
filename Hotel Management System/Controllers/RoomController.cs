using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System.Linq;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin,FrontDesk")]
    public class RoomController : Controller
    {
        private readonly HotelManagementDbContext _context;

        public RoomController(HotelManagementDbContext context)
        {
            _context = context;
        }

        public IActionResult Rooms()
        {
            var rooms = _context.Rooms.Include(r => r.Bookings).ToList(); // ✅ Ensure all related bookings are fetched
            return View(rooms);
        }

        public IActionResult AvailableRooms()
        {
            var availableRooms = _context.Rooms
                .Include(r => r.Bookings) // ✅ Fetch related bookings
                .Where(r => r.Status == "Available")
                .ToList();
            return View(availableRooms);
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult BookedRooms()
        {
            var bookedRooms = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Confirmed" || b.Status == "Checked-In")
                .ToList();

            return View(bookedRooms);
        }


        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ReservedRooms()
        {
            var reservedRooms = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Reserved")
                .ToList();

            return View(reservedRooms);
        }

    }
}