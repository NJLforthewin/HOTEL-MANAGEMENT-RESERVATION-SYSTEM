using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Hotel_Management_System.Services;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin, FrontDesk")]
    public class FrontDeskController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly EmailService _emailService;
        // Update your constructor to include the email service
        public FrontDeskController(HotelManagementDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

        public IActionResult Dashboard()
        {
            try
            {
                // Get bookings
                var pendingBookings = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b => b.Status != null && b.Status.Trim() == "Pending")
                    .OrderByDescending(b => b.CheckInDate)
                    .ToList();

                var confirmedBookings = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b => b.Status != null && b.Status.Trim() == "Confirmed" && b.CheckedInAt == null)
                    .OrderByDescending(b => b.CheckInDate)
                    .ToList();

                // Calculate statistics for dashboard
                var today = DateTime.Today;

                // Today's arrivals
                var todayArrivals = _context.Bookings
                    .Count(b => b.CheckInDate.Date == today &&
                               (b.Status == "Confirmed" || b.Status == "Pending"));

                // Today's departures
                var todayDepartures = _context.Bookings
                    .Count(b => b.CheckOutDate.Date == today &&
                               b.CheckedInAt != null && b.CheckedOutAt == null);

                // Current occupancy
                var totalRooms = _context.Rooms.Count();
                var occupiedRooms = _context.Bookings
                    .Count(b => b.CheckedInAt != null && b.CheckedOutAt == null &&
                               b.CheckOutDate.Date > today);

                int occupancyRate = totalRooms > 0 ? (occupiedRooms * 100) / totalRooms : 0;

                // Set ViewBag data
                ViewBag.PendingBookings = pendingBookings;
                ViewBag.ConfirmedBookings = confirmedBookings;
                ViewBag.TodayArrivals = todayArrivals;
                ViewBag.TodayDepartures = todayDepartures;
                ViewBag.Occupancy = occupancyRate;
                ViewBag.PendingCount = pendingBookings.Count;

                Debug.WriteLine("[INFO] Dashboard data loaded successfully");
                return View("Dashboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error loading dashboard: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View("Dashboard");
            }
        }

        [HttpGet]
        public IActionResult WalkIn(int? roomId = null)
        {
            var availableRooms = _context.Rooms
                .Where(r => r.Status == "Available")
                .OrderBy(r => r.RoomNumber)
                .ToList();

            ViewBag.AvailableRooms = availableRooms;
            ViewBag.SelectedRoomId = roomId;

            // Create a dictionary of room prices manually
            var roomPrices = new Dictionary<string, decimal>();
            foreach (var room in availableRooms)
            {
                roomPrices[room.RoomId.ToString()] = room.PricePerNight;
            }
            ViewBag.RoomPrices = roomPrices;

            if (roomId.HasValue)
            {
                var selectedRoom = _context.Rooms.FirstOrDefault(r => r.RoomId == roomId);
                if (selectedRoom != null)
                {
                    ViewBag.SelectedRoom = selectedRoom;
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessWalkIn(Booking booking, int roomId)
        {
            try
            {
                var room = _context.Rooms.Find(roomId);
                if (room == null || room.Status != "Available")
                {
                    TempData["ErrorMessage"] = "Selected room is not available.";
                    return RedirectToAction("WalkIn");
                }

                booking.RoomId = roomId;
                booking.CreatedAt = DateTime.Now;
                booking.Status = "Checked-In";
                booking.CheckedInAt = DateTime.Now;
                booking.BookingType = "Walk-In";
                booking.PaymentStatus = "Paid";

                // Generate a transaction ID based on payment method
                booking.TransactionId = $"{booking.PaymentMethod?.Replace("/", "").Replace(" ", "").ToUpper() ?? "CASH"}-{Guid.NewGuid().ToString().Substring(0, 8)}";

                _context.Bookings.Add(booking);

                // Update room status
                room.Status = "Occupied";

                await _context.SaveChangesAsync();

                // Send confirmation email if email is provided
                if (!string.IsNullOrEmpty(booking.Email) && booking.Email != "No Email")
                {
                    try
                    {
                        await _emailService.SendBookingConfirmationAsync(
                            booking.Email,
                            booking.GuestName ?? "Valued Guest",
                            booking.BookingId.ToString(),
                            booking.CheckInDate,
                            booking.CheckOutDate,
                            room.RoomNumber,
                            booking.TotalPrice,
                            booking.PaymentMethod ?? "Cash" // Use the selected payment method
                        );

                        Debug.WriteLine($"Confirmation email sent to {booking.Email}");
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't stop the flow
                        Debug.WriteLine($"Error sending email: {ex.Message}");
                    }
                }

                TempData["SuccessMessage"] = "Walk-in booking created successfully!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error processing walk-in: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while processing the walk-in booking.";
                return RedirectToAction("WalkIn");
            }
        }

        [HttpGet]
        public IActionResult AvailableRooms(string category = "", string sortBy = "roomNumber")
        {
            try
            {
                // Start with rooms that have "Available" status
                var query = _context.Rooms.Where(r => r.Status == "Available");

                // Apply category filter if provided
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(r => r.Category == category);
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "price" => query.OrderBy(r => r.PricePerNight),
                    "category" => query.OrderBy(r => r.Category),
                    "capacity" => query.OrderBy(r => r.Capacity),
                    _ => query.OrderBy(r => r.RoomNumber)
                };

                var availableRooms = query.ToList();

                // Prepare for the view
                ViewBag.Categories = _context.Rooms
                    .Select(r => r.Category)
                    .Where(c => c != null)
                    .Distinct()
                    .ToList();
                ViewBag.SelectedCategory = category;
                ViewBag.SortBy = sortBy;
                ViewBag.RoomCount = availableRooms.Count;

                return View(availableRooms);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading available rooms: " + ex.Message;
                return View(new List<Room>());
            }
        }

        [HttpGet]
        public IActionResult BookedRooms(DateTime? checkOutDate = null, string sortBy = "roomNumber")
        {
            try
            {
                // Get checked-in bookings
                var query = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b => b.Status == "Checked-In" && b.CheckedOutAt == null);

                // Filter by checkout date if provided
                if (checkOutDate.HasValue)
                {
                    query = query.Where(b => b.CheckOutDate.Date == checkOutDate.Value.Date);
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "checkoutdate" => query.OrderBy(b => b.CheckOutDate),
                    "guestname" => query.OrderBy(b => b.GuestName),
                    _ => query.OrderBy(b => b.Room != null ? b.Room.RoomNumber : string.Empty)
                };

                var bookedRooms = query.ToList();

                // Prepare for the view
                ViewBag.CheckOutDate = checkOutDate;
                ViewBag.SortBy = sortBy;
                ViewBag.BookingCount = bookedRooms.Count;
                ViewBag.TodayCheckOuts = bookedRooms.Count(b => b.CheckOutDate.Date == DateTime.Today);

                return View(bookedRooms);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading booked rooms: " + ex.Message;
                return View(new List<Booking>());
            }
        }

        [HttpGet]
        public IActionResult ReservedRooms(DateTime? arrivalDate = null, string sortBy = "arrivaldate")
        {
            try
            {
                // Get confirmed bookings that haven't checked in yet
                var query = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b => (b.Status == "Confirmed" || b.Status == "Reserved") && b.CheckedInAt == null);

                // Filter by arrival date if provided
                if (arrivalDate.HasValue)
                {
                    query = query.Where(b => b.CheckInDate.Date == arrivalDate.Value.Date);
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "guestname" => query.OrderBy(b => b.GuestName),
                    "roomnumber" => query.OrderBy(b => b.Room != null ? b.Room.RoomNumber : string.Empty),
                    _ => query.OrderBy(b => b.CheckInDate)
                };

                var reservedRooms = query.ToList();

                // Get all distinct arrival dates for filter dropdown
                var arrivalDates = _context.Bookings
                    .Where(b => (b.Status == "Confirmed" || b.Status == "Reserved") && b.CheckedInAt == null)
                    .Select(b => b.CheckInDate.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                // Prepare for the view
                ViewBag.ArrivalDates = arrivalDates;
                ViewBag.SelectedDate = arrivalDate;
                ViewBag.SortBy = sortBy;
                ViewBag.BookingCount = reservedRooms.Count;
                ViewBag.TodayArrivals = reservedRooms.Count(b => b.CheckInDate.Date == DateTime.Today);

                return View(reservedRooms);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading reserved rooms: " + ex.Message;
                return View(new List<Booking>());
            }
        }

        [HttpGet]
        public IActionResult AllRooms(string status = "", string category = "", string sortBy = "roomNumber")
        {
            try
            {
                // Start with all rooms
                var query = _context.Rooms.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(r => r.Status == status);
                }

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(r => r.Category == category);
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "price" => query.OrderBy(r => r.PricePerNight),
                    "category" => query.OrderBy(r => r.Category),
                    "status" => query.OrderBy(r => r.Status),
                    _ => query.OrderBy(r => r.RoomNumber)
                };

                var rooms = query.ToList();

                // Get all distinct categories and statuses for filter dropdowns
                var categories = _context.Rooms
                    .Select(r => r.Category)
                    .Where(c => c != null)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                var statuses = _context.Rooms
                    .Select(r => r.Status)
                    .Where(s => s != null)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                // For rooms that are occupied, find their current bookings
                var occupiedRoomIds = rooms
                    .Where(r => r.Status == "Occupied")
                    .Select(r => r.RoomId)
                    .ToList();

                var currentBookings = _context.Bookings
                    .Where(b => b.Status == "Checked-In" && occupiedRoomIds.Contains(b.RoomId))
                    .ToDictionary(b => b.RoomId);

                // Prepare for the view
                ViewBag.Categories = categories;
                ViewBag.Statuses = statuses;
                ViewBag.SelectedCategory = category;
                ViewBag.SelectedStatus = status;
                ViewBag.SortBy = sortBy;
                ViewBag.RoomCount = rooms.Count;
                ViewBag.CurrentBookings = currentBookings;

                return View(rooms);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading rooms: " + ex.Message;
                return View(new List<Room>());
            }
        }

        [HttpGet]
        public IActionResult BookingDetails(int id)
        {
            try
            {
                var booking = _context.Bookings
                    .Include(b => b.Room)
                    .FirstOrDefault(b => b.BookingId == id);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Booking not found!";
                    return RedirectToAction("Dashboard");
                }

                return View(booking);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error loading booking details: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading booking details.";
                return RedirectToAction("Dashboard");
            }
        }

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

            if (booking.Status != null && booking.Status.Trim() == "Pending")
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

        [HttpPost]
        public IActionResult ProcessCheckout(string roomNumber)
        {
            try
            {
                // Since RoomNumber is already a string in your model, we can compare directly
                var room = _context.Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found!";
                    return RedirectToAction("Dashboard");
                }

                var booking = _context.Bookings
                    .Include(b => b.Room)
                    .FirstOrDefault(b => b.Room != null && b.Room.RoomId == room.RoomId &&
                                         b.CheckedInAt != null && b.CheckedOutAt == null);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "No active booking found for this room!";
                    return RedirectToAction("Dashboard");
                }

                // Process checkout
                booking.Status = "Checked Out";
                booking.CheckedOutAt = DateTime.Now;

                if (booking.Room != null)
                {
                    booking.Room.Status = "Cleaning";
                    // We need to handle nullable differently for DateTimes vs strings
                    // For MaintenanceNotes which is non-nullable, use empty string
                    // For LastCleaned which is nullable DateTime, use null
                    booking.Room.MaintenanceNotes = "Needs cleaning after checkout";
                    booking.Room.LastCleaned = null;
                }

                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Room {roomNumber} has been checked out successfully!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error processing checkout: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred during checkout.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin, FrontDesk")]
        public IActionResult ConfirmCheckIn(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("Dashboard", "FrontDesk");
            }

            // Add debug output to see current status
            System.Diagnostics.Debug.WriteLine($"Current booking status: {booking.Status}");

            // Check current status before changing
            if (booking.Status != "Confirmed" && booking.Status != "Pending" && booking.Status != "Reserved")
            {
                TempData["ErrorMessage"] = $"Cannot check in booking with status: {booking.Status}";
                return RedirectToAction("Dashboard", "FrontDesk");
            }

            // Try updating with one of these valid values
            try
            {
                // Option 1: "Checked-In" with hyphen
                booking.Status = "Checked-In";
                booking.CheckedInAt = DateTime.Now;

                if (booking.Room != null)
                {
                    booking.Room.Status = "Occupied";
                }

                _context.SaveChanges();
                TempData["SuccessMessage"] = "Check-in successful!";
            }
            catch (Exception ex1)
            {
                // If that fails, try Option 2: "Checked In" without hyphen
                try
                {
                    booking.Status = "Checked In";
                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "Check-in successful!";
                }
                catch (Exception ex2)
                {
                    // If both fail, try Option 3: Just use status code "C"
                    try
                    {
                        booking.Status = "C"; // Compact code for checked in
                        _context.SaveChanges();
                        TempData["SuccessMessage"] = "Check-in successful!";
                    }
                    catch (Exception ex3)
                    {
                        // Log all exceptions
                        System.Diagnostics.Debug.WriteLine($"Failed with Checked-In: {ex1.Message}");
                        System.Diagnostics.Debug.WriteLine($"Failed with Checked In: {ex2.Message}");
                        System.Diagnostics.Debug.WriteLine($"Failed with C: {ex3.Message}");

                        TempData["ErrorMessage"] = "Could not update booking status due to constraint. Please check database constraint on Status field.";
                    }
                }
            }

            return RedirectToAction("Dashboard", "FrontDesk");
        }

        [HttpPost]
        public IActionResult CancelBooking(int id)
        {
            try
            {
                var booking = _context.Bookings
                    .Include(b => b.Room)
                    .FirstOrDefault(b => b.BookingId == id);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Booking not found!";
                    return RedirectToAction("ReservedRooms");
                }

                if (booking.Room != null && (booking.Status == "Confirmed" || booking.Status == "Pending"))
                {
                    booking.Room.Status = "Available";
                }

                _context.Bookings.Remove(booking);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Booking canceled successfully!";
                return RedirectToAction("ReservedRooms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error canceling booking: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while canceling the booking.";
                return RedirectToAction("ReservedRooms");
            }
        }

        [HttpPost]
        public IActionResult ProcessCheckin(int bookingId)
        {
            return ConfirmCheckIn(bookingId);
        }
    }
}