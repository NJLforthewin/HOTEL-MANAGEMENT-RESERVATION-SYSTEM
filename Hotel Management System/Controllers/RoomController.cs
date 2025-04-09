using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System.Linq;

namespace Hotel_Management_System.Controllers
{
    // Move the Authorize attribute to individual actions instead of controller level
    public class RoomController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly ILogger<RoomController> _logger;

        public RoomController(HotelManagementDbContext context, ILogger<RoomController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize(Roles = "Admin,FrontDesk")]
        public IActionResult Rooms()
        {
            var rooms = _context.Rooms.Include(r => r.Bookings).ToList();
            return View(rooms);
        }

        [Authorize(Roles = "Admin,FrontDesk")]
        public IActionResult AvailableRooms()
        {
            var availableRooms = _context.Rooms
                .Include(r => r.Bookings)
                .Where(r => r.Status == "Available")
                .ToList();
            return View(availableRooms);
        }

        [Authorize(Roles = "Admin,FrontDesk")]
        public IActionResult BookedRooms()
        {
            var bookedRooms = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Confirmed" || b.Status == "Checked-In")
                .ToList();

            return View(bookedRooms);
        }

        [Authorize(Roles = "Admin,FrontDesk")]
        public IActionResult ReservedRooms()
        {
            var reservedRooms = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Reserved")
                .ToList();

            return View(reservedRooms);
        }

        // This action should be accessible to everyone
        public IActionResult Details(int id)
        {
            _logger.LogInformation($"Fetching details for room ID: {id}");

            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);

            if (room == null)
            {
                _logger.LogWarning($"Room with ID {id} not found");
                return NotFound();
            }

            _logger.LogInformation($"Found room: {room.RoomId}, {room.Category}, {room.RoomNumber}");
            return View(room);
        }
    }
}