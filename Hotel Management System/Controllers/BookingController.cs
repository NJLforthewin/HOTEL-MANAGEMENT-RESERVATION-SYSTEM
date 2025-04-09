using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Hotel_Management_System.Controllers
{
    public class BookingController : Controller
    {
        private readonly HotelManagementDbContext _context;
        public BookingController(HotelManagementDbContext context)
        {
            _context = context;
        }

        // GET method for Create
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Create(int? roomId = null)
        {
            
            // For authenticated users, show available rooms
            var rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
            ViewBag.Rooms = rooms;
            ViewBag.RoomPrices = rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);

            if (roomId.HasValue)
            {
                ViewBag.SelectedRoomId = roomId.Value;
            }
            else if (TempData["SelectedRoomId"] != null)
            {
                if (TempData["SelectedRoomId"] is int selectedRoomId)
                {
                    ViewBag.SelectedRoomId = selectedRoomId;
                }
                TempData.Keep("SelectedRoomId");
            }

            return View();
        }

        // POST method for Create
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Booking booking)
        {
            // Debug info to find validation errors
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                System.Diagnostics.Debug.WriteLine($"Model Error: {error.ErrorMessage}");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            try
            {
                // Force UserId to null
                booking.UserId = null;

                ProcessBooking(booking);

                // Redirect based on BookingType
                if (booking.BookingType == "Walk-In")
                {
                    return RedirectToAction("BookedRooms", "Room"); // Redirect for Walk-In bookings
                }
                return RedirectToAction("Index", "Home"); // Normal booking goes to Home
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error processing booking: " + ex.Message);
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }
        }
        private void ProcessBooking(Booking booking)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
            if (room == null)
            {
                ModelState.AddModelError("", "Selected room not found.");
                throw new Exception("Selected room not found.");
            }

            if (booking.CheckInDate >= booking.CheckOutDate)
            {
                ModelState.AddModelError("", "Check-out date must be after check-in date.");
                throw new Exception("Check-out date must be after check-in date.");
            }

            // IMPORTANT: Set UserId to null directly to avoid any lookup
            booking.UserId = null;

            // Ensure we have values for required fields
            booking.GuestName ??= "Guest";
            booking.Email ??= "No Email";
            booking.PhoneNumber ??= "No Phone";
            booking.TotalPrice = CalculateTotalPrice(room.PricePerNight, booking.CheckInDate, booking.CheckOutDate);
            booking.PaymentStatus = "Unpaid";
            booking.PaymentMethod ??= "Not Specified";
            booking.CheckedOutAt = null;
            booking.CreatedAt = DateTime.Now;

            // Handle Walk-In Booking separately
            if (booking.BookingType == "Walk-In")
            {
                booking.Status = "Checked-In";   // Directly check-in
                booking.CheckedInAt = DateTime.Now;
                if (room != null)
                {
                    room.Status = "Occupied";  // Mark room as occupied
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
            var rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
            ViewBag.Rooms = rooms;
            ViewBag.RoomPrices = rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);

            // Create booking without requiring a user
            var booking = new Booking
            {
                BookingType = "Walk-In",
                CreatedAt = DateTime.Now
            };

            // Try to find a user, but don't require one
            try
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrEmpty(email))
                {
                    var currentUser = _context.Users.FirstOrDefault(u => u.Email == email);
                    if (currentUser != null)
                    {
                        booking.UserId = currentUser.UserId;
                        booking.User = currentUser;
                    }
                }
            }
            catch (Exception ex)
            {
                // Just log the exception but continue
                System.Diagnostics.Debug.WriteLine($"Error finding user: {ex.Message}");
            }

            return View(booking);
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WalkInBooking(Booking booking)
        {
            // Debug info to find validation errors
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                System.Diagnostics.Debug.WriteLine($"Model Error: {error.ErrorMessage}");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            try
            {
                var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
                if (room == null)
                {
                    ModelState.AddModelError("", "Selected room not found.");
                    ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                    ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                    return View(booking);
                }

                if (booking.CheckInDate >= booking.CheckOutDate)
                {
                    ModelState.AddModelError("", "Check-out date must be after check-in date.");
                    ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                    ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                    return View(booking);
                }

                // Try to find a user, but don't require one
                try
                {
                    var email = User.FindFirstValue(ClaimTypes.Email);
                    if (!string.IsNullOrEmpty(email))
                    {
                        var currentUser = _context.Users.FirstOrDefault(u => u.Email == email);
                        if (currentUser != null)
                        {
                            booking.UserId = currentUser.UserId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Just log the exception but continue
                    System.Diagnostics.Debug.WriteLine($"Error finding user: {ex.Message}");
                }

                // Set default values if not provided
                booking.GuestName ??= "Guest";
                booking.Email ??= "No Email";
                booking.PhoneNumber ??= "No Phone";
                booking.TotalPrice = CalculateTotalPrice(room.PricePerNight, booking.CheckInDate, booking.CheckOutDate);
                booking.PaymentMethod ??= "Not Specified";
                booking.CreatedAt = DateTime.Now;

                // Walk-In Booking is immediately confirmed
                booking.BookingType = "Walk-In";
                booking.Status = "Checked-In";
                booking.PaymentStatus = "Paid";
                booking.CheckedInAt = DateTime.Now;
                booking.CheckedOutAt = null;

                // Mark Room as Occupied
                room.Status = "Occupied";

                _context.Bookings.Add(booking);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Walk-In Booking successful! Guest is now checked in.";
                return RedirectToAction("BookedRooms", "Room"); // Fixed action name
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error processing booking: " + ex.Message);
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }
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
                booking.Status = "Reserved";  // Move to Reserved instead of directly to Booking
            }
            else
            {
                booking.Status = "Checked-In";  // Regular bookings follow normal flow
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
            {
                booking.Room.Status = "Occupied";
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Check-in successful!";
            return RedirectToAction("Dashboard", "FrontDesk");
        }
        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult CheckOut(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null || booking.Status != "Checked-In")
            {
                TempData["ErrorMessage"] = "Invalid checkout request!";
                return RedirectToAction("ActiveBookings", "Booking");
            }

            booking.Status = "Checked-Out";
            booking.CheckedOutAt = DateTime.Now;

            if (booking.Room != null)
            {
                booking.Room.Status = "Needs Cleaning"; // This is already correct
                booking.Room.LastCleaned = booking.CheckedOutAt;
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Checkout successful!";
            return RedirectToAction("Dashboard", "FrontDesk");
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmedBookings()
        {
            var confirmedBookings = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Confirmed" && b.CheckedInAt == null)
                .OrderBy(b => b.CheckInDate)
                .ToList();

            return View(confirmedBookings);
        }

        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ActiveBookings()
        {
            var activeBookings = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Checked-In" && b.CheckedOutAt == null)
                .OrderBy(b => b.CheckOutDate)
                .ToList();

            return View(activeBookings);
        }

        private decimal CalculateTotalPrice(decimal pricePerNight, DateTime checkIn, DateTime checkOut)
        {
            int nights = (int)(checkOut - checkIn).TotalDays;
            return pricePerNight * nights;
        }
    }
}