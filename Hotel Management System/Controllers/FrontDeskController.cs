using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System.Linq;
using System.Diagnostics;

namespace Hotel_Management_System.Controllers
{
    public class FrontDeskController : Controller
    {
        private readonly HotelManagementDbContext _context;

        public FrontDeskController(HotelManagementDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Create()
        {
            var rooms = _context.Rooms.ToList();
            ViewBag.Rooms = rooms;
            ViewBag.RoomPrices = rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
            return View();
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult Dashboard()
        {
            var pendingBookings = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status.Trim() == "Pending")
                .OrderByDescending(b => b.CheckInDate)
                .ToList();

            var confirmedBookings = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status.Trim() == "Confirmed")
                .OrderByDescending(b => b.CheckInDate)
                .ToList();

            ViewBag.PendingBookings = pendingBookings;
            ViewBag.ConfirmedBookings = confirmedBookings;

            return View("Dashboard");
        }

        

        // CONFIRM BOOKING (Moves to confirmed bookings)
        [Authorize(Roles = "Admin, FrontDesk")]
        [HttpPost]
        public IActionResult Confirm(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                Debug.WriteLine($"[ERROR] Booking ID {bookingId} not found.");
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard");
            }

            if (booking.Status.Trim() == "Pending")
            {
                booking.Status = "Confirmed";
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Booking confirmed successfully!";
                Debug.WriteLine($"[SUCCESS] Booking ID {bookingId} confirmed.");
            }
            else
            {
                Debug.WriteLine($"[WARNING] Booking ID {bookingId} could not be confirmed. Current status: {booking.Status}");
                TempData["ErrorMessage"] = "Booking cannot be confirmed!";
            }

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        [HttpPost]
        public IActionResult Cancel(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                Debug.WriteLine($"[ERROR] Booking ID {bookingId} not found.");
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard");
            }

            if (booking.Room != null)
            {
                booking.Room.Status = "Available";
            }

            _context.Bookings.Remove(booking);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Booking has been successfully deleted.";
            Debug.WriteLine($"[SUCCESS] Booking ID {bookingId} deleted.");
            return RedirectToAction("Dashboard");
        }

    }
}
