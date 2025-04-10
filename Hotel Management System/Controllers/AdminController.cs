using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hotel_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Diagnostics;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(HotelManagementDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            // Get current time and previous month
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            // Calculate total bookings
            var totalBookings = _context.Bookings.Count();
            var lastMonthBookings = _context.Bookings
                .Count(b => b.CreatedAt >= startOfLastMonth && b.CreatedAt <= endOfLastMonth);
            var currentMonthBookings = _context.Bookings
                .Count(b => b.CreatedAt >= startOfMonth && b.CreatedAt <= endOfMonth);
            var bookingGrowth = lastMonthBookings > 0
                ? (int)Math.Round(((decimal)(currentMonthBookings - lastMonthBookings) / lastMonthBookings) * 100m)
                : 0;

            // Calculate occupied rooms
            var totalRooms = _context.Rooms.Count();
            var occupiedRooms = _context.Rooms.Count(r => r.Status == "Occupied" || r.Status == "Booked");
            var occupancyRate = totalRooms > 0
                ? (int)Math.Round(((decimal)occupiedRooms / totalRooms) * 100m)
                : 0;

            // Calculate total revenue
            var totalRevenue = _context.Bookings
                .Where(b => b.PaymentStatus == "Paid")
                .Sum(b => (decimal?)b.TotalPrice) ?? 0m;
            var lastMonthRevenue = _context.Bookings
                .Where(b => b.PaymentStatus == "Paid" && b.CreatedAt >= startOfLastMonth && b.CreatedAt <= endOfLastMonth)
                .Sum(b => (decimal?)b.TotalPrice) ?? 0m;
            var currentMonthRevenue = _context.Bookings
                .Where(b => b.PaymentStatus == "Paid" && b.CreatedAt >= startOfMonth && b.CreatedAt <= endOfMonth)
                .Sum(b => (decimal?)b.TotalPrice) ?? 0m;
            var revenueGrowth = lastMonthRevenue > 0
                ? (int)Math.Round(((currentMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100m)
                : 0;

            // Calculate user stats
            var totalUsers = _context.Users.Count();
            var lastMonthUsers = _context.Users
                .Count(u => u.CreatedAt >= startOfLastMonth && u.CreatedAt <= endOfLastMonth);
            var currentMonthUsers = _context.Users
                .Count(u => u.CreatedAt >= startOfMonth && u.CreatedAt <= endOfMonth);
            var userGrowth = lastMonthUsers > 0
                ? (int)Math.Round(((decimal)(currentMonthUsers - lastMonthUsers) / lastMonthUsers) * 100m)
                : 0;

            // Get recent bookings
            var recentBookings = _context.Bookings
                .Include(b => b.Room)
                .OrderByDescending(b => b.BookingId)
                .Take(5)
                .ToList();

            // Get upcoming checkouts
            var upcomingCheckouts = _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.Status == "Checked-In" && b.CheckOutDate >= today)
                .OrderBy(b => b.CheckOutDate)
                .Take(3)
                .ToList();

            // Set ViewBag properties
            ViewBag.TotalBookings = totalBookings;
            ViewBag.BookingGrowth = bookingGrowth;
            ViewBag.OccupiedRooms = occupiedRooms;
            ViewBag.OccupancyRate = occupancyRate;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.RevenueGrowth = revenueGrowth;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.UserGrowth = userGrowth;
            ViewBag.RecentBookings = recentBookings;
            ViewBag.UpcomingCheckouts = upcomingCheckouts;

            return View();
        }

        public IActionResult UserList()
        {
            var users = _context.Users.ToList();
            return View(users);
        }

        public IActionResult PromoteToAdmin(int userId)
        {
            int adminCount = _context.Users.Count(u => u.Role == "Admin");

            if (adminCount >= 4)
            {
                TempData["ErrorMessage"] = "Cannot have more than 4 admins!";
                return RedirectToAction("UserList");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user != null && user.Role != "Admin")
            {
                user.Role = "Admin";
                _context.SaveChanges();
            }

            return RedirectToAction("UserList");
        }

        public IActionResult DemoteToGuest(int userId)
        {
            int adminCount = _context.Users.Count(u => u.Role == "Admin");

            if (adminCount <= 1)
            {
                TempData["ErrorMessage"] = "At least one admin is required!";
                return RedirectToAction("UserList");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user != null && user.Role == "Admin")
            {
                user.Role = "Guest";
                _context.SaveChanges();
            }

            return RedirectToAction("UserList");
        }

        public async Task<IActionResult> CreateBooking()
        {
            try
            {
                // Get available rooms
                var availableRooms = await _context.Rooms
                    .Where(r => r.Status == "Available")
                    .Select(r => new { r.RoomId, r.RoomNumber, r.Category, r.PricePerNight }) // Use PricePerNight
                    .ToListAsync();

                // Get payment methods
                var paymentMethods = new List<string> { "Cash", "Credit Card", "Debit Card", "Bank Transfer" };

                // Set default dates
                ViewBag.CheckinDate = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.CheckoutDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                ViewBag.AvailableRooms = availableRooms;
                ViewBag.PaymentMethods = paymentMethods;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin booking page");
                TempData["ErrorMessage"] = "An error occurred while loading the booking page: " + ex.Message;
                return RedirectToAction("Index", "Admin");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(Booking booking)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Set defaults for new booking
                    booking.CreatedAt = DateTime.Now; // Using CreatedAt instead of BookingDate
                    booking.Status = "Confirmed";
                    // No CreatedBy field in the model - removed that line

                    // Get room details
                    var room = await _context.Rooms.FindAsync(booking.RoomId);
                    if (room == null)
                    {
                        ModelState.AddModelError("RoomId", "Selected room not found");
                        return View(booking);
                    }

                    // Calculate total price if not set
                    if (booking.TotalPrice <= 0)
                    {
                        var nights = (booking.CheckOutDate - booking.CheckInDate).Days; // Use CheckInDate/CheckOutDate
                        booking.TotalPrice = room.PricePerNight * nights; // Use PricePerNight
                    }

                    // Update room status
                    room.Status = "Occupied";
                    _context.Rooms.Update(room);

                    // Add booking
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Booking created successfully.";
                    return RedirectToAction("Bookings", "Admin");
                }

                // If we got this far, something failed, redisplay form
                var availableRooms = await _context.Rooms
                    .Where(r => r.Status == "Available" || r.RoomId == booking.RoomId)
                    .Select(r => new { r.RoomId, r.RoomNumber, r.Category, r.PricePerNight }) // Use PricePerNight
                    .ToListAsync();

                var paymentMethods = new List<string> { "Cash", "Credit Card", "Debit Card", "Bank Transfer" };

                ViewBag.AvailableRooms = availableRooms;
                ViewBag.PaymentMethods = paymentMethods;

                return View(booking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                TempData["ErrorMessage"] = "An error occurred while creating the booking: " + ex.Message;

                // Repopulate necessary data
                var availableRooms = await _context.Rooms
                    .Where(r => r.Status == "Available" || r.RoomId == booking.RoomId)
                    .Select(r => new { r.RoomId, r.RoomNumber, r.Category, r.PricePerNight }) // Use PricePerNight
                    .ToListAsync();

                var paymentMethods = new List<string> { "Cash", "Credit Card", "Debit Card", "Bank Transfer" };

                ViewBag.AvailableRooms = availableRooms;
                ViewBag.PaymentMethods = paymentMethods;

                return View(booking);
            }
        }

        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateUser(string firstName, string lastName, string email, string password, string role)
        {
            if (role != "FrontDesk" && role != "Housekeeping")
            {
                TempData["ErrorMessage"] = "Invalid role selection!";
                return RedirectToAction("UserList");
            }

            var newUser = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "User created successfully!";
            return RedirectToAction("UserList");
        }

        public IActionResult DeleteUser(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "User deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "User not found!";
            }

            return RedirectToAction("UserList");
        }

        [HttpPost]
        public IActionResult MakeAllRoomsAvailable()
        {
            var rooms = _context.Rooms.ToList();
            foreach (var room in rooms)
            {
                room.Status = "Available";
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = "All rooms have been set to Available.";
            return RedirectToAction("Rooms");
        }

        public IActionResult Rooms()
        {
            try
            {
                var rooms = _context.Rooms.ToList();

                foreach (var room in rooms)
                {
                    room.Category = room.Category ?? "Standard";
                    room.Status = room.Status ?? "Available";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                foreach (var room in rooms)
                {
                    room.Bookings = _context.Bookings
                        .Where(b => b.RoomId == room.RoomId)
                        .ToList();
                }

                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rooms in admin panel");
                TempData["ErrorMessage"] = "An error occurred while loading rooms.";
                return View(new List<Room>());
            }
        }

        public IActionResult AvailableRooms()
        {
            try
            {
                // Fetch all rooms first
                var allRooms = _context.Rooms.ToList();

                // Handle NULL values and filter in memory
                foreach (var room in allRooms)
                {
                    room.Category = room.Category ?? "Standard";
                    room.Status = room.Status ?? "Available";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                // Filter in memory
                var availableRooms = allRooms
                    .Where(r => r.Status == "Available")
                    .ToList();

                // Load related bookings manually
                foreach (var room in availableRooms)
                {
                    room.Bookings = _context.Bookings
                        .Where(b => b.RoomId == room.RoomId)
                        .ToList();
                }

                return View(availableRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available rooms");
                TempData["ErrorMessage"] = "An error occurred while loading available rooms.";
                return View(new List<Room>());
            }
        }

        

        

        public IActionResult BookedRooms()
        {
            try
            {
                // Fetch all bookings first
                var allBookings = _context.Bookings.ToList();

                // Filter in memory
                var bookedBookings = allBookings
                    .Where(b => b.Status == "Confirmed" || b.Status == "Checked-In")
                    .ToList();

                // Load rooms for each booking
                foreach (var booking in bookedBookings)
                {
                    booking.Room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);

                    // Handle NULL values
                    if (booking.Room != null)
                    {
                        booking.Room.Category = booking.Room.Category ?? "Standard";
                        booking.Room.Status = booking.Room.Status ?? "Booked";
                        booking.Room.MaintenanceNotes = booking.Room.MaintenanceNotes ?? "";
                    }
                }

                return View(bookedBookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading booked rooms");
                TempData["ErrorMessage"] = "An error occurred while loading booked rooms.";
                return View(new List<Booking>());
            }
        }

        public IActionResult ReservedRooms()
        {
            try
            {
                var allBookings = _context.Bookings.ToList();

                var reservedBookings = allBookings
                    .Where(b => b.Status == "Reserved")
                    .ToList();

                foreach (var booking in reservedBookings)
                {
                    booking.Room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);

                    if (booking.Room != null)
                    {
                        booking.Room.Category = booking.Room.Category ?? "Standard";
                        booking.Room.Status = booking.Room.Status ?? "Reserved";
                        booking.Room.MaintenanceNotes = booking.Room.MaintenanceNotes ?? "";
                    }
                }

                return View(reservedBookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reserved rooms");
                TempData["ErrorMessage"] = "An error occurred while loading reserved rooms.";
                return View(new List<Booking>());
            }
        }
        public IActionResult BookingDashboard()
        {
            ViewBag.PendingBookings = _context.Bookings
                                        .Where(b => b.Status == "Pending")
                                        .Include(b => b.Room)
                                        .ToList();

            ViewBag.ConfirmedBookings = _context.Bookings
                                        .Where(b => b.Status == "Confirmed")
                                        .Include(b => b.Room)
                                        .ToList();

            ViewBag.BookedRooms = _context.Rooms
                                        .Where(r => r.Status == "Booked" || r.Status == "Occupied")
                                        .ToList();

            ViewBag.ReservedRooms = _context.Rooms
                                        .Where(r => r.Status == "Reserved")
                                        .ToList();

            return View();
        }

        public IActionResult Reports(string reportType = "occupancy", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Set default date range if not provided
                DateTime actualStartDate = startDate ?? DateTime.Today.AddMonths(-1);
                DateTime actualEndDate = endDate ?? DateTime.Today;
                ViewBag.StartDate = actualStartDate.ToString("yyyy-MM-dd");
                ViewBag.EndDate = actualEndDate.ToString("yyyy-MM-dd");
                ViewBag.ReportType = reportType;
                // Get different reports based on type
                switch (reportType)
                {
                    case "revenue":
                        GenerateRevenueReport(actualStartDate, actualEndDate);
                        break;
                    case "bookings":
                        GenerateBookingsReport(actualStartDate, actualEndDate);
                        break;
                    case "guests":
                        GenerateGuestsReport(actualStartDate, actualEndDate);
                        break;
                    case "occupancy":
                    default:
                        GenerateOccupancyReport(actualStartDate, actualEndDate);
                        break;
                }
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reports");
                TempData["ErrorMessage"] = "An error occurred while generating reports.";
                return View();
            }
        }

        private void GenerateOccupancyReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Calculate days in range
                int daysInRange = (int)(endDate - startDate).TotalDays + 1;

                // Get all rooms
                var rooms = _context.Rooms.ToList();
                foreach (var room in rooms)
                {
                    room.Category = room.Category ?? "Standard";
                    room.Status = room.Status ?? "Available";
                }

                int totalRooms = rooms.Count;

                // Get all bookings in range
                var bookings = _context.Bookings
                    .Where(b =>
                        (b.CheckInDate <= endDate && b.CheckOutDate >= startDate) &&
                        (b.Status == "Confirmed" || b.Status == "Checked-In"))
                    .ToList();

                // Calculate overall occupancy rate
                decimal totalRoomDays = totalRooms * daysInRange;

                // Calculate occupied room days by summing up the duration of each booking within the date range
                int occupiedRoomDays = 0;
                foreach (var booking in bookings)
                {
                    // Adjust dates to be within range
                    DateTime effectiveStart = booking.CheckInDate < startDate ? startDate : booking.CheckInDate;
                    DateTime effectiveEnd = booking.CheckOutDate > endDate ? endDate : booking.CheckOutDate;

                    // Add the number of days this booking occupied a room within our range
                    occupiedRoomDays += (int)(effectiveEnd - effectiveStart).TotalDays + 1;
                }

                decimal occupancyRate = totalRoomDays > 0 ?
                    Math.Round((occupiedRoomDays / totalRoomDays) * 100, 2) : 0;

                // Group by room category
                var categoryOccupancy = rooms
                    .GroupBy(r => r.Category)
                    .Select(g => new
                    {
                        Category = g.Key,
                        TotalRooms = g.Count(),
                        BookedRooms = bookings
                            .Count(b => rooms.FirstOrDefault(r => r.RoomId == b.RoomId)?.Category == g.Key)
                    })
                    .ToList();

                // Group by date for trend
                var dateOccupancy = new List<object>();
                for (var day = startDate; day <= endDate; day = day.AddDays(1))
                {
                    var dayBookings = bookings
                        .Count(b => b.CheckInDate <= day && b.CheckOutDate >= day);

                    dateOccupancy.Add(new
                    {
                        Date = day.ToString("MM/dd"),
                        OccupancyRate = Math.Round((decimal)dayBookings / totalRooms * 100, 2)
                    });
                }

                ViewBag.TotalRooms = totalRooms;
                ViewBag.OccupiedRoomDays = occupiedRoomDays;
                ViewBag.TotalRoomDays = totalRoomDays;
                ViewBag.OccupancyRate = occupancyRate;
                ViewBag.CategoryOccupancy = categoryOccupancy;
                ViewBag.DateOccupancy = dateOccupancy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating occupancy report");
            }
        }

        private void GenerateRevenueReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Get all bookings in date range with payment status "Paid"
                var bookings = _context.Bookings
                    .Where(b => b.CreatedAt >= startDate && b.CreatedAt <= endDate && b.PaymentStatus == "Paid")
                    .ToList();

                // Total revenue in range
                decimal totalRevenue = bookings.Sum(b => b.TotalPrice);

                // Average revenue per booking
                decimal avgBookingValue = bookings.Count > 0 ?
                    Math.Round(totalRevenue / bookings.Count, 2) : 0;

                // Revenue by payment method
                var revenueByPayment = bookings
                    .GroupBy(b => string.IsNullOrEmpty(b.PaymentMethod) ? "Other" : b.PaymentMethod)
                    .Select(g => new
                    {
                        Method = g.Key,
                        Amount = g.Sum(b => b.TotalPrice),
                        Percentage = Math.Round((g.Sum(b => b.TotalPrice) / totalRevenue) * 100, 2)
                    })
                    .ToList();

                // Revenue by room category
                var revenueByCategory = bookings
                    .GroupBy(b => _context.Rooms.FirstOrDefault(r => r.RoomId == b.RoomId)?.Category ?? "Unknown")
                    .Select(g => new
                    {
                        Category = g.Key,
                        Amount = g.Sum(b => b.TotalPrice),
                        Percentage = Math.Round((g.Sum(b => b.TotalPrice) / totalRevenue) * 100, 2)
                    })
                    .ToList();

                // Revenue trend by date
                var revenueTrend = bookings
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("MM/dd"),
                        Amount = g.Sum(b => b.TotalPrice)
                    })
                    .OrderBy(x => DateTime.ParseExact(x.Date, "MM/dd", null))
                    .ToList();

                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.AvgBookingValue = avgBookingValue;
                ViewBag.BookingCount = bookings.Count;
                ViewBag.RevenueByPayment = revenueByPayment;
                ViewBag.RevenueByCategory = revenueByCategory;
                ViewBag.RevenueTrend = revenueTrend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating revenue report");
            }
        }

        private void GenerateBookingsReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Get all bookings in date range
                var bookings = _context.Bookings
                    .Where(b => b.CreatedAt >= startDate && b.CreatedAt <= endDate)
                    .ToList();

                // Total bookings
                int totalBookings = bookings.Count;

                // Bookings by status
                var bookingsByStatus = bookings
                    .GroupBy(b => string.IsNullOrEmpty(b.Status) ? "Unknown" : b.Status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        Percentage = Math.Round(((decimal)g.Count() / totalBookings) * 100, 2)
                    })
                    .ToList();

                // Bookings by payment status
                var bookingsByPayment = bookings
                    .GroupBy(b => string.IsNullOrEmpty(b.PaymentStatus) ? "Unknown" : b.PaymentStatus)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        Percentage = Math.Round(((decimal)g.Count() / totalBookings) * 100, 2)
                    })
                    .ToList();

                // Average stay duration
                double avgStayDuration = bookings.Count > 0 ?
                    Math.Round(bookings.Average(b => (b.CheckOutDate - b.CheckInDate).TotalDays), 1) : 0;

                // Bookings by day of week
                var bookingsByDay = bookings
                    .GroupBy(b => b.CreatedAt.DayOfWeek)
                    .Select(g => new
                    {
                        Day = g.Key.ToString(),
                        Count = g.Count()
                    })
                    .OrderBy(x => Enum.Parse<DayOfWeek>(x.Day))
                    .ToList();

                // Booking trend by date
                var bookingTrend = bookings
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("MM/dd"),
                        Count = g.Count()
                    })
                    .OrderBy(x => DateTime.ParseExact(x.Date, "MM/dd", null))
                    .ToList();

                ViewBag.TotalBookings = totalBookings;
                ViewBag.BookingsByStatus = bookingsByStatus;
                ViewBag.BookingsByPayment = bookingsByPayment;
                ViewBag.AvgStayDuration = avgStayDuration;
                ViewBag.BookingsByDay = bookingsByDay;
                ViewBag.BookingTrend = bookingTrend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bookings report");
            }
        }

        private void GenerateGuestsReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Get all bookings in date range
                var bookings = _context.Bookings
                    .Where(b => b.CreatedAt >= startDate && b.CreatedAt <= endDate)
                    .ToList();

                // Use GuestName for unique guests instead of UserId 
                var guestNames = bookings.Select(b => b.GuestName).Distinct().ToList();

                // Total unique guests
                int totalGuests = guestNames.Count;

                // For new guests, get the earliest booking date for each guest
                var guestFirstBookings = bookings
                    .GroupBy(b => b.GuestName)
                    .Select(g => new {
                        GuestName = g.Key,
                        FirstBookingDate = g.Min(b => b.CreatedAt)
                    })
                    .ToList();

                // New guests are those whose first booking is within our date range
                var newGuests = guestFirstBookings

                    .Count(g => g.FirstBookingDate >= startDate && g.FirstBookingDate <= endDate);

                // Returning guests
                var returningGuests = totalGuests - newGuests;

                // Bookings per guest
                decimal bookingsPerGuest = totalGuests > 0 ?
                    Math.Round((decimal)bookings.Count / totalGuests, 2) : 0;

                // Top guests by booking count
                var topGuests = bookings
                    .GroupBy(b => b.GuestName)
                    .Select(g => new
                    {
                        Name = g.Key,
                        BookingCount = g.Count(),
                        TotalSpent = g.Sum(b => b.TotalPrice)
                    })
                    .OrderByDescending(x => x.BookingCount)
                    .Take(5)
                    .ToList();

                ViewBag.TotalGuests = totalGuests;
                ViewBag.NewGuests = newGuests;
                ViewBag.ReturningGuests = returningGuests;
                ViewBag.BookingsPerGuest = bookingsPerGuest;
                ViewBag.TopGuests = topGuests;
                ViewBag.NewGuestPercentage = totalGuests > 0 ?
                    Math.Round(((decimal)newGuests / totalGuests) * 100, 2) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating guests report");
            }
        }

        [HttpPost]
        public IActionResult ConfirmBooking(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                _logger.LogError($"Booking ID {bookingId} not found.");
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("BookingDashboard");
            }

            if (booking.Status.Trim() == "Pending")
            {
                booking.Status = "Confirmed";
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Booking confirmed successfully!";
                _logger.LogInformation($"Booking ID {bookingId} confirmed.");
            }
            else
            {
                _logger.LogWarning($"Booking ID {bookingId} could not be confirmed. Current status: {booking.Status}");
                TempData["ErrorMessage"] = "Booking cannot be confirmed!";
            }

            return RedirectToAction("BookingDashboard");
        }

        [HttpPost]
        public IActionResult CancelBooking(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                _logger.LogError($"Booking ID {bookingId} not found.");
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("BookingDashboard");
            }

            if (booking.Room != null)
            {
                booking.Room.Status = "Available";
            }

            _context.Bookings.Remove(booking);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Booking has been successfully deleted.";
            _logger.LogInformation($"Booking ID {bookingId} deleted.");
            return RedirectToAction("BookingDashboard");
        }

        [HttpPost]
        public IActionResult ConfirmPayment(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found!";
                return RedirectToAction("BookingDashboard");
            }

            booking.PaymentStatus = "Paid";
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Payment confirmed!";
            return RedirectToAction("BookingDashboard");
        }

        [HttpPost]
        public IActionResult ConfirmCheckIn(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null || booking.Status != "Confirmed")
            {
                TempData["ErrorMessage"] = "Invalid check-in request!";
                return RedirectToAction("BookingDashboard");
            }

            booking.Status = booking.BookingType == "Reservation" ? "Reserved" : "Checked-In";
            booking.CheckedInAt = DateTime.Now;

            if (booking.Room != null)
                booking.Room.Status = "Occupied"; // Mark room as occupied

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Guest has been checked in successfully!";
            return RedirectToAction("BookingDashboard");
        }

        [HttpPost]
        public IActionResult ConfirmCheckOut(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == bookingId);

            if (booking == null || (booking.Status != "Checked-In" && booking.Status != "Reserved"))
            {
                TempData["ErrorMessage"] = "Invalid check-out request!";
                return RedirectToAction("BookingDashboard");
            }

            if (booking.Room != null)
                booking.Room.Status = "Available"; // Mark room as available

            _context.Bookings.Remove(booking); // Delete booking after checkout
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Guest has checked out successfully!";
            return RedirectToAction("BookingDashboard");
        }

        public IActionResult WalkInBooking()
        {
            var availableRooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
            ViewBag.Rooms = availableRooms;
            ViewBag.RoomPrices = availableRooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);

            var booking = new Booking
            {
                BookingType = "Walk-In",
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1)
            };

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WalkInBooking(Booking booking)
        {
            if (!ModelState.IsValid)
            {
                var availableRooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.Rooms = availableRooms;
                ViewBag.RoomPrices = availableRooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
            if (room == null || booking.CheckInDate >= booking.CheckOutDate)
            {
                ModelState.AddModelError("", "Invalid booking details.");
                var availableRooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.Rooms = availableRooms;
                ViewBag.RoomPrices = availableRooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(booking);
            }

            // Walk-In Booking is immediately confirmed
            booking.BookingType = "Walk-In";
            booking.Status = "Checked-In";
            booking.PaymentStatus = "Paid";
            booking.CheckedInAt = DateTime.Now;
            booking.CheckedOutAt = null;
            booking.TotalPrice = CalculateTotalPrice(room.PricePerNight, booking.CheckInDate, booking.CheckOutDate);

            // Mark Room as Occupied
            room.Status = "Occupied";

            _context.Bookings.Add(booking);

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Walk-In Booking successful! Guest is now checked in.";
            return RedirectToAction("Rooms");
        }

        public async Task<IActionResult> HousekeepingDashboard()
        {
            try
            {
                // Get counts for different statuses
                var rooms = new List<Room>();

                try
                {
                    // Fetch rooms one by one to avoid SQL null issues
                    rooms = await _context.Rooms
                        .AsNoTracking() // This can help with performance
                        .ToListAsync();

                    // Handle null values
                    foreach (var room in rooms)
                    {
                        room.Status = room.Status ?? "Available";
                        room.Category = room.Category ?? "Standard";
                        room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                        // Handle any other potential null strings
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching rooms: " + ex.Message);
                    // Continue with empty room list
                }

                // Updated variable names to match the view
                ViewBag.AvailableCount = rooms.Count(r => r.Status == "Available");
                ViewBag.OccupiedCount = rooms.Count(r => r.Status == "Occupied" || r.Status == "Booked");
                ViewBag.NeedsCleaningCount = rooms.Count(r => r.Status == "Needs Cleaning");
                ViewBag.MaintenanceCount = rooms.Count(r => r.Status == "Maintenance");

                // Get recent cleaning records
                var recentCleanings = new List<Room>();

                try
                {
                    recentCleanings = await _context.Rooms
                        .AsNoTracking()
                        .Where(r => r.LastCleaned.HasValue)
                        .OrderByDescending(r => r.LastCleaned)
                        .Take(5)
                        .ToListAsync();

                    // Handle null values
                    foreach (var room in recentCleanings)
                    {
                        room.Status = room.Status ?? "Available";
                        room.Category = room.Category ?? "Standard";
                        room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching recent cleanings: " + ex.Message);
                    // Continue with empty list
                }

                ViewBag.RecentCleanings = recentCleanings;

                // Get housekeeping staff and their assignments
                try
                {
                    var housekeepingStaff = await _context.HousekeepingStaff
                        .Where(s => s.IsActive)
                        .ToListAsync();

                    // Get active assignments
                    var activeAssignments = await _context.HousekeepingAssignments
                        .Include(a => a.Room)
                        .Include(a => a.Staff)
                        .Where(a => a.Status != "Completed")
                        .ToListAsync();

                    ViewBag.ActiveAssignments = activeAssignments;

                    // Create a dictionary to store tuples of status and count for each staff
                    var staffStatus = new Dictionary<int, Tuple<string, int>>();

                    // Calculate status for each staff member
                    foreach (var staff in housekeepingStaff)
                    {
                        var staffAssignmentCount = activeAssignments.Count(a => a.StaffId == staff.StaffId);

                        string status;
                        if (staffAssignmentCount == 0)
                        {
                            status = "Available";
                        }
                        else if (staffAssignmentCount >= 5)
                        {
                            status = "Busy";
                        }
                        else
                        {
                            status = "Working";
                        }

                        staffStatus[staff.StaffId] = new Tuple<string, int>(status, staffAssignmentCount);
                    }

                    ViewBag.HousekeepingStaff = housekeepingStaff;
                    ViewBag.StaffCount = housekeepingStaff.Count;   
                    ViewBag.StaffStatus = staffStatus;

                    // Add chart data for monthly cleaning performance
                    var labels = new List<string>() { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
                    var completedData = new List<int>() { 30, 28, 32, 27, 34, 29 };
                    var assignedData = new List<int>() { 35, 32, 37, 31, 38, 36 };
                    var pendingData = new List<int>() { 5, 4, 5, 4, 4, 7 };

                    ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(labels);
                    ViewBag.CompletedData = System.Text.Json.JsonSerializer.Serialize(completedData);
                    ViewBag.AssignedData = System.Text.Json.JsonSerializer.Serialize(assignedData);
                    ViewBag.PendingData = System.Text.Json.JsonSerializer.Serialize(pendingData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching housekeeping staff and assignments: " + ex.Message);
                    ViewBag.HousekeepingStaff = new List<HousekeepingStaff>();
                    ViewBag.StaffCount = 0;
                    ViewBag.ActiveAssignments = new List<HousekeepingAssignment>();
                    ViewBag.StaffStatus = new Dictionary<int, Tuple<string, int>>();
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HousekeepingDashboard: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the housekeeping dashboard: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        public IActionResult HousekeepingAdmin()
        {
            try
            {
                // Get counts for different statuses
                var rooms = _context.Rooms.ToList();

                foreach (var room in rooms)
                {
                    room.Status = room.Status ?? "Available";
                    room.Category = room.Category ?? "Standard";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                ViewBag.AvailableCount = rooms.Count(r => r.Status == "Available");
                ViewBag.OccupiedCount = rooms.Count(r => r.Status == "Occupied" || r.Status == "Booked");
                ViewBag.NeedsCleaningCount = rooms.Count(r => r.Status == "Needs Cleaning");
                ViewBag.MaintenanceCount = rooms.Count(r => r.Status == "Maintenance");

                // Get housekeeping staff
                var housekeepingStaff = _context.Users
                    .Where(u => u.Role == "Housekeeping")
                    .ToList();

                // Handle potential null values
                foreach (var staff in housekeepingStaff)
                {
                    staff.FirstName = staff.FirstName ?? "";
                    staff.LastName = staff.LastName ?? "";
                    staff.Email = staff.Email ?? "";
                }

                ViewBag.StaffCount = housekeepingStaff.Count;
                ViewBag.HousekeepingStaff = housekeepingStaff;

                // Get recent cleaning records
                var recentCleanings = _context.Rooms
                    .Where(r => r.LastCleaned.HasValue)
                    .OrderByDescending(r => r.LastCleaned)
                    .Take(5)
                    .ToList();

                // Handle potential null values
                foreach (var room in recentCleanings)
                {
                    room.Status = room.Status ?? "Available";
                    room.Category = room.Category ?? "Standard";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                ViewBag.RecentCleanings = recentCleanings;

                return View("HousekeepingDashboard"); // Use the existing view
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageHousekeeping()
        {
            try
            {
                var rooms = await _context.Rooms.ToListAsync();

                foreach (var room in rooms)
                {
                    room.Status = room.Status ?? "Available";
                    room.Category = room.Category ?? "Standard";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manage housekeeping");
                TempData["ErrorMessage"] = "An error occurred while loading the housekeeping management page.";
                return RedirectToAction("Dashboard");
            }
        }



        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignHousekeeping(int roomId)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    return NotFound();
                }

                // Get housekeeping staff
                var housekeepingStaff = await _context.Users
                    .Where(u => u.Role == "Housekeeping")
                    .ToListAsync();

                ViewBag.HousekeepingStaff = housekeepingStaff;

                return View(room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assign housekeeping");
                TempData["ErrorMessage"] = "An error occurred while loading the assignment page.";
                return RedirectToAction("ManageHousekeeping");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignHousekeepingStaff(int roomId, int staffId)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    return NotFound();
                }

                var staff = await _context.Users.FindAsync(staffId);
                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Selected staff member not found.";
                    return RedirectToAction("AssignHousekeeping", new { roomId });
                }

                // In a complete system, you'd have a HousekeepingAssignment table
                // For now, we'll use MaintenanceNotes to track assignment
                room.MaintenanceNotes = $"Assigned to {staff.FirstName} {staff.LastName} on {DateTime.Now:yyyy-MM-dd HH:mm}. " + room.MaintenanceNotes;

                if (room.Status == "Available")
                {
                    room.Status = "Needs Cleaning";
                }

                _context.Update(room);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room {room.RoomNumber} assigned to {staff.FirstName} {staff.LastName}.";
                return RedirectToAction("ManageHousekeeping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning housekeeping staff");
                TempData["ErrorMessage"] = "An error occurred while assigning staff.";
                return RedirectToAction("AssignHousekeeping", new { roomId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkRoomStatus(int roomId, string status)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    return NotFound();
                }

                // Validate status
                var validStatuses = new[] { "Available", "Needs Cleaning", "Maintenance" };
                if (!validStatuses.Contains(status))
                {
                    TempData["ErrorMessage"] = "Invalid room status.";
                    return RedirectToAction(nameof(ManageHousekeeping));
                }

                room.Status = status;
                if (status == "Available")
                {
                    room.LastCleaned = DateTime.Now;
                }

                _context.Update(room);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room {room.RoomNumber} status updated to {status}.";
                return RedirectToAction("ManageHousekeeping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room status");
                TempData["ErrorMessage"] = "An error occurred while updating room status.";
                return RedirectToAction("ManageHousekeeping");
            }
        }




        public IActionResult AssignHousekeeping(int? roomId)
        {
            // Get all housekeeping staff
            var housekeepingStaff = _context.HousekeepingStaff
                .Where(s => s.IsActive)
                .ToList();

            ViewBag.HousekeepingStaff = housekeepingStaff;

            // If roomId provided, get that specific room
            if (roomId.HasValue)
            {
                var room = _context.Rooms.Find(roomId.Value);
                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found.";
                    return RedirectToAction(nameof(ManageHousekeeping));
                }

                // Get current assignment if exists
                var currentAssignment = _context.HousekeepingAssignments
                    .Include(a => a.Staff)
                    .Where(a => a.RoomId == roomId.Value && a.Status != "Completed")
                    .FirstOrDefault();

                ViewBag.Room = room;
                ViewBag.CurrentAssignment = currentAssignment;

                return View("AssignHousekeepingToRoom");
            }

            // Otherwise, show all rooms for assignment
            var rooms = _context.Rooms.ToList();
            ViewBag.Rooms = rooms;

            // Get all active assignments
            var activeAssignments = _context.HousekeepingAssignments
                .Include(a => a.Room)
                .Include(a => a.Staff)
                .Where(a => a.Status != "Completed")
                .ToList();

            ViewBag.ActiveAssignments = activeAssignments;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignStaffToRoom(int roomId, int staffId, string priority, string notes)
        {
            try
            {
                var room = _context.Rooms.Find(roomId);
                var staff = _context.HousekeepingStaff.Find(staffId);

                if (room == null || staff == null)
                {
                    TempData["ErrorMessage"] = "Room or staff member not found.";
                    return RedirectToAction(nameof(AssignHousekeeping));
                }

                // Check if room already has an active assignment
                var existingAssignment = _context.HousekeepingAssignments
                    .Where(a => a.RoomId == roomId && a.Status != "Completed")
                    .FirstOrDefault();

                if (existingAssignment != null)
                {
                    // Update existing assignment
                    existingAssignment.StaffId = staffId;
                    existingAssignment.Priority = !string.IsNullOrEmpty(priority) ? priority : "Normal";
                    existingAssignment.Notes = notes ?? string.Empty;
                    existingAssignment.AssignedAt = DateTime.Now;
                    existingAssignment.Status = "Assigned";

                    _context.Update(existingAssignment);
                }
                else
                {
                    // Create new assignment
                    var assignment = new HousekeepingAssignment
                    {
                        RoomId = roomId,
                        StaffId = staffId,
                        Priority = !string.IsNullOrEmpty(priority) ? priority : "Normal",
                        Notes = notes ?? string.Empty,
                        AssignedAt = DateTime.Now,
                        Status = "Assigned"
                    };

                    _context.HousekeepingAssignments.Add(assignment);
                }

                _context.SaveChanges();

                TempData["SuccessMessage"] = $"Successfully assigned {staff.FirstName} {staff.LastName} to Room {room.RoomNumber}.";
                return RedirectToAction(nameof(ManageHousekeeping));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error assigning staff: " + ex.Message;
                return RedirectToAction(nameof(AssignHousekeeping));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteAssignment(int assignmentId)
        {
            try
            {
                var assignment = _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Staff)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId);

                if (assignment == null)
                {
                    TempData["ErrorMessage"] = "Assignment not found.";
                    return RedirectToAction(nameof(ManageHousekeeping));
                }

                // Mark assignment as completed
                assignment.Status = "Completed";
                assignment.CompletedAt = DateTime.Now;

                // Update room status to Available
                assignment.Room.Status = "Available";
                assignment.Room.LastCleaned = DateTime.Now;

                _context.Update(assignment);
                _context.SaveChanges();

                TempData["SuccessMessage"] = $"Assignment completed. Room {assignment.Room.RoomNumber} is now available.";
                return RedirectToAction(nameof(ManageHousekeeping));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error completing assignment: " + ex.Message;
                return RedirectToAction(nameof(ManageHousekeeping));
            }
        }

        // GET: Admin/AddHousekeepingStaff
        public IActionResult AddHousekeepingStaff()
        {
            return View();
        }

        // POST: Admin/AddHousekeepingStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHousekeepingStaff(HousekeepingStaff model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Set default values
                    model.IsActive = true;

                    // Add to database
                    _context.HousekeepingStaff.Add(model);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff member added successfully!";
                    return RedirectToAction("HousekeepingDashboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding housekeeping staff");
                    ModelState.AddModelError("", "Error adding staff member: " + ex.Message);
                }
            }

            return View(model);
        }

        // GET: Admin/AssignTasks
        public async Task<IActionResult> AssignTasks(int staffId)
        {
            try
            {
                // Get the staff member
                var staff = await _context.HousekeepingStaff
                    .FirstOrDefaultAsync(s => s.StaffId == staffId);

                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Staff member not found.";
                    return RedirectToAction("HousekeepingDashboard");
                }

                // Get rooms that need cleaning
                var roomsNeedingCleaning = await _context.Rooms
                    .Where(r => r.Status == "Needs Cleaning")
                    .ToListAsync();

                // Get existing assignments for this staff member
                var existingAssignments = await _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .Where(a => a.StaffId == staffId && a.Status != "Completed")
                    .ToListAsync();

                ViewBag.Staff = staff;
                ViewBag.RoomsNeedingCleaning = roomsNeedingCleaning;
                ViewBag.ExistingAssignments = existingAssignments;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AssignTasks: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the task assignment page: " + ex.Message;
                return RedirectToAction("HousekeepingDashboard");
            }
        }

        // POST: Admin/AssignTaskToStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTaskToStaff(int staffId, int roomId, string priority, string notes)
        {
            try
            {
                // Validate that the staff exists
                var staff = await _context.HousekeepingStaff.FindAsync(staffId);
                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Staff member not found.";
                    return RedirectToAction("HousekeepingDashboard");
                }

                // Check staff's current workload
                var currentAssignmentCount = await _context.HousekeepingAssignments
                    .CountAsync(a => a.StaffId == staffId && a.Status != "Completed");

                // Validate that the room exists and needs cleaning
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found.";
                    return RedirectToAction("AssignTasks", new { staffId });
                }

                // Check if this room is already assigned to someone
                var existingAssignment = await _context.HousekeepingAssignments
                    .FirstOrDefaultAsync(a => a.RoomId == roomId && a.Status != "Completed");

                if (existingAssignment != null)
                {
                    TempData["ErrorMessage"] = $"Room {room.RoomNumber} is already assigned to another staff member.";
                    return RedirectToAction("AssignTasks", new { staffId });
                }

                // Create and save the assignment
                var assignment = new HousekeepingAssignment
                {
                    StaffId = staffId,
                    RoomId = roomId,
                    Priority = string.IsNullOrEmpty(priority) ? "Normal" : priority,
                    Notes = notes ?? string.Empty,
                    Status = "Assigned",
                    AssignedAt = DateTime.Now
                };

                _context.HousekeepingAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                string message = $"Room {room.RoomNumber} has been assigned to {staff.FirstName} {staff.LastName}.";

                // Add a warning if staff has multiple tasks
                if (currentAssignmentCount >= 4) // Now has 5 or more tasks including the new one
                {
                    TempData["WarningMessage"] = $"Note: This staff member now has {currentAssignmentCount + 1} active tasks and may be overloaded.";
                }

                TempData["SuccessMessage"] = message;
                return RedirectToAction("AssignTasks", new { staffId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AssignTaskToStaff: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while assigning the task: " + ex.Message;
                return RedirectToAction("AssignTasks", new { staffId });
            }
        }

        // POST: Admin/RemoveAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment(int assignmentId, int staffId)
        {
            try
            {
                var assignment = await _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

                if (assignment == null)
                {
                    TempData["ErrorMessage"] = "Assignment not found.";
                    return RedirectToAction("AssignTasks", new { staffId });
                }

                _context.HousekeepingAssignments.Remove(assignment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Assignment for Room {assignment.Room.RoomNumber} has been removed.";
                return RedirectToAction("AssignTasks", new { staffId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveAssignment: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while removing the assignment: " + ex.Message;
                return RedirectToAction("AssignTasks", new { staffId });
            }
        }

        // GET: Admin/EditHousekeepingStaff
        public async Task<IActionResult> EditHousekeepingStaff(int id)
        {
            try
            {
                var staff = await _context.HousekeepingStaff.FindAsync(id);
                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Staff member not found.";
                    return RedirectToAction("HousekeepingDashboard");
                }

                return View(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching staff for edit: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading staff details: " + ex.Message;
                return RedirectToAction("HousekeepingDashboard");
            }
        }

        // POST: Admin/EditHousekeepingStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHousekeepingStaff(HousekeepingStaff staff)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingStaff = await _context.HousekeepingStaff.FindAsync(staff.StaffId);
                    if (existingStaff == null)
                    {
                        TempData["ErrorMessage"] = "Staff member not found.";
                        return RedirectToAction("HousekeepingDashboard");
                    }

                    // Update properties
                    existingStaff.FirstName = staff.FirstName;
                    existingStaff.LastName = staff.LastName;
                    existingStaff.Email = staff.Email;
                    existingStaff.PhoneNumber = staff.PhoneNumber;
                    existingStaff.Position = staff.Position;
                    existingStaff.IsActive = staff.IsActive;

                    _context.Update(existingStaff);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff information updated successfully.";
                    return RedirectToAction("HousekeepingDashboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating staff: " + ex.Message);
                    ModelState.AddModelError("", "An error occurred while updating staff information: " + ex.Message);
                }
            }

            return View(staff);
        }

        // POST: Admin/DeleteHousekeepingStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHousekeepingStaff(int id)
        {
            try
            {
                var staff = await _context.HousekeepingStaff.FindAsync(id);
                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Staff member not found.";
                    return RedirectToAction("HousekeepingDashboard");
                }

                // Check if staff has active assignments
                var activeAssignments = await _context.HousekeepingAssignments
                    .Where(a => a.StaffId == id && a.Status != "Completed")
                    .CountAsync();

                if (activeAssignments > 0)
                {
                    TempData["ErrorMessage"] = $"Cannot delete staff member with {activeAssignments} active assignments. Reassign or complete these tasks first.";
                    return RedirectToAction("EditHousekeepingStaff", new { id });
                }

                // Delete the staff member
                _context.HousekeepingStaff.Remove(staff);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Staff member deleted successfully.";
                return RedirectToAction("HousekeepingDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting staff: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while deleting staff member: " + ex.Message;
                return RedirectToAction("EditHousekeepingStaff", new { id });
            }
        }



        private static decimal CalculateTotalPrice(decimal pricePerNight, DateTime checkInDate, DateTime checkOutDate)
        {
            int totalDays = (checkOutDate - checkInDate).Days;
            return totalDays > 0 ? pricePerNight * totalDays : pricePerNight;
        }

    }
}