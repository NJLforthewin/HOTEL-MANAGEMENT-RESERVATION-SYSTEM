using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Hotel_Management_System.Controllers
{
    public class BookingController : Controller
    {
        private readonly HotelManagementDbContext _context;

        public BookingController(HotelManagementDbContext context)
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

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Booking booking)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = _context.Rooms.ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            ProcessBooking(booking);

            // ✅ Redirect based on BookingType
            if (booking.BookingType == "Walk-In")
            {
                return RedirectToAction("BookedRooms", "Room"); // ✅ Redirect for Walk-In bookings
            }

            return RedirectToAction("Index", "Home"); // ✅ Normal booking goes to Home
        }


        private void ProcessBooking(Booking booking)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
            if (room == null || booking.CheckInDate >= booking.CheckOutDate)
            {
                ModelState.AddModelError("", "Invalid booking details.");
                return;
            }

            booking.GuestName ??= "Guest";
            booking.Email ??= "No Email";
            booking.PhoneNumber ??= "No Phone";
            booking.TotalPrice = CalculateTotalPrice(room.PricePerNight, booking.CheckInDate, booking.CheckOutDate);
            booking.PaymentStatus = "Unpaid";
            booking.PaymentMethod ??= "Not Specified";
            booking.CheckedOutAt = null;

            // ✅ Handle Walk-In Booking separately
            if (booking.BookingType == "Walk-In")
            {
                booking.Status = "Checked-In";   // 🔥 Directly check-in
                booking.CheckedInAt = DateTime.Now;

                if (room != null)
                {
                    room.Status = "Occupied";  // 🔥 Mark room as occupied
                }
            }
            else
            {
                booking.Status = "Pending";   // Normal flow for other bookings
                booking.CheckedInAt = null;
            }

            _context.Bookings.Add(booking);
            _context.SaveChanges();

            TempData["SuccessMessage"] = booking.BookingType == "Walk-In"
                ? "Walk-in booking confirmed! Guest has been checked in."
                : "Booking submitted successfully!";
        }


        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult DashboardBooking()
        {
            return RedirectToAction("Dashboard", "FrontDesk");
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        [HttpGet]
        public IActionResult WalkInBooking()
        {
            var rooms = _context.Rooms.Where(r => r.Status == "Available").ToList(); // ✅ Only show available rooms
            ViewBag.Rooms = rooms;
            ViewBag.RoomPrices = rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);

            var booking = new Booking
            {
                BookingType = "Walk-In"  // ✅ Always Walk-In
            };

            return View(booking);
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WalkInBooking(Booking booking)
        {
            if (!ModelState.IsValid)
            {

                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
            if (room == null || booking.CheckInDate >= booking.CheckOutDate)
            {
                ModelState.AddModelError("", "Invalid booking details.");
                return View(booking);
            }

            // ✅ Walk-In Booking is immediately confirmed
            booking.BookingType = "Walk-In";
            booking.Status = "Checked-In";
            booking.PaymentStatus = "Paid";
            booking.CheckedInAt = DateTime.Now;
            booking.CheckedOutAt = null;

            // ✅ Mark Room as Occupied
            room.Status = "Occupied";

            _context.Bookings.Add(booking);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Walk-In Booking successful! Guest is now checked in.";
            return RedirectToAction("BookedRooms", "Rooms");
        }


        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmBooking(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard", "FrontDesk");
            }

            if (booking.BookingType == "Reservation")
            {
                booking.Status = "Reserved";  // ✅ Move to Reserved instead of directly to Booking
            }
            else
            {
                booking.Status = "Checked-In";  // ✅ Regular bookings follow normal flow
            }
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Booking confirmed!";
            return RedirectToAction("Dashboard", "FrontDesk");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmPayment(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard", "FrontDesk");
            }

            booking.PaymentStatus = "Paid";
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Payment confirmed!";
            return RedirectToAction("Dashboard", "FrontDesk");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmCheckIn(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null || booking.Status != "Confirmed")
            {
                TempData["ErrorMessage"] = "Invalid check-in request!";
                return RedirectToAction("ConfirmedBookings", "Booking");
            }

            booking.Status = booking.BookingType == "Reservation" ? "Reserved" : "Checked-In";
            booking.CheckedInAt = DateTime.Now;

            if (booking.Room != null)
                booking.Room.Status = "Occupied"; // Mark room as occupied

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Guest has been checked in successfully!";
            return RedirectToAction(booking.BookingType == "Reservation" ? "ReservedRooms" : "BookedRooms", "Room");
        }


        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmCheckOut(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);

            if (booking == null || (booking.Status != "Checked-In" && booking.Status != "Reserved"))
            {
                TempData["ErrorMessage"] = "Invalid check-out request!";
                return RedirectToAction("ReservedRooms", "Room"); // Redirect back to ReservedRooms page
            }

            if (booking.Room != null)
                booking.Room.Status = "Available"; // ✅ Mark room as available

            _context.Bookings.Remove(booking); // ✅ Delete booking after checkout
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Guest has checked out successfully!";
            return RedirectToAction(booking.BookingType == "Reservation" ? "ReservedRooms" : "BookedRooms", "Room");
        }



        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult CancelBooking(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard", "FrontDesk");
            }

            booking.Status = "Canceled";
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Booking has been canceled.";
            return RedirectToAction("Dashboard", "FrontDesk");
        }

        private static decimal CalculateTotalPrice(decimal pricePerNight, DateTime checkInDate, DateTime checkOutDate)
        {
            int totalDays = (checkOutDate - checkInDate).Days;
            return totalDays > 0 ? pricePerNight * totalDays : pricePerNight;
        }
    }
}
