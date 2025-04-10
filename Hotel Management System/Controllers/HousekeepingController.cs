using Hotel_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin,Housekeeping")]
    public class HousekeepingController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly ILogger<HousekeepingController> _logger;
        public HousekeepingController(HotelManagementDbContext context, ILogger<HousekeepingController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<IActionResult> Index()
        {
            try
            {
                // First fix any null values in the database
                await FixNullValuesInDatabase();

                // Get all rooms with their current status
                var rooms = await _context.Rooms.ToListAsync() ?? new List<Room>();

                // Get all active assignments
                var assignments = await _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Staff)
                    .Where(a => a.Status != "Completed")
                    .ToListAsync();

                // Create a dictionary to map roomId to staff name
                var roomAssignments = new Dictionary<int, string>();
                foreach (var assignment in assignments)
                {
                    if (assignment.Staff != null)
                    {
                        roomAssignments[assignment.RoomId] = assignment.Staff.FirstName + " " + assignment.Staff.LastName;
                    }
                }

                // Set ViewBag
                ViewBag.AllRooms = rooms;
                ViewBag.RoomAssignments = roomAssignments;

                // Handle null values for display
                foreach (var room in rooms)
                {
                    room.Status = room.Status ?? "Available";
                    room.Category = room.Category ?? "Standard";
                    room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                }

                // Prepare statistics for the dashboard
                ViewBag.NeedsCleaning = rooms.Count(r => r.Status == "Needs Cleaning");
                ViewBag.Maintenance = rooms.Count(r => r.Status == "Maintenance");
                ViewBag.Available = rooms.Count(r => r.Status == "Available");
                ViewBag.Occupied = rooms.Count(r => r.Status == "Occupied");

                // Set these for the navigation badges
                ViewBag.PendingRoomAssignments = ViewBag.NeedsCleaning;
                ViewBag.MaintenanceRequestsCount = ViewBag.Maintenance;

                // Get rooms that need attention based on recent checkouts
                List<Booking> recentCheckouts = new List<Booking>();
                try
                {
                    var yesterday = DateTime.Now.AddDays(-1);
                    recentCheckouts = await _context.Bookings
                        .Where(b => b.Status == "Checked-Out" && b.CheckedOutAt >= yesterday)
                        .Include(b => b.Room)
                        .OrderByDescending(b => b.CheckedOutAt)
                        .Take(5)
                        .ToListAsync() ?? new List<Booking>();

                    // Filter out any nulls
                    recentCheckouts = recentCheckouts.Where(c => c.Room != null).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading recent checkouts");
                }

                ViewBag.RecentCheckouts = recentCheckouts;
                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rooms");
                TempData["ErrorMessage"] = "An error occurred while loading rooms: " + ex.Message;

                // Create empty lists to avoid null reference exceptions
                ViewBag.AllRooms = new List<Room>();
                ViewBag.RecentCheckouts = new List<Booking>();
                ViewBag.RoomAssignments = new Dictionary<int, string>();
                ViewBag.NeedsCleaning = 0;
                ViewBag.Maintenance = 0;
                ViewBag.Available = 0;
                ViewBag.Occupied = 0;
                ViewBag.PendingRoomAssignments = 0;
                ViewBag.MaintenanceRequestsCount = 0;

                return View(new List<Room>());
            }
        }

        // Private method to fix null values in the database
        private async Task FixNullValuesInDatabase()
        {
            try
            {
                // Use SQL to update null values directly
                await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE Rooms 
                      SET Category = 'Standard' WHERE Category IS NULL;
                      UPDATE Rooms 
                      SET Status = 'Available' WHERE Status IS NULL;
                      UPDATE Rooms 
                      SET MaintenanceNotes = '' WHERE MaintenanceNotes IS NULL;"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing null values in database");
            }
        }

        // Redirect for Dashboard to handle possible navigation errors
        public IActionResult Dashboard()
        {
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> RoomDetails(int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    return NotFound();
                }

                // Handle null values
                room.Category = room.Category ?? "Standard";
                room.Status = room.Status ?? "Available";
                room.MaintenanceNotes = room.MaintenanceNotes ?? "";

                // Get current booking (if room is occupied)
                var currentBooking = await _context.Bookings
                    .Where(b => b.RoomId == id && b.Status == "Checked-In")
                    .FirstOrDefaultAsync();

                // Get booking history for this room
                var bookingHistory = await _context.Bookings
                    .Where(b => b.RoomId == id)
                    .OrderByDescending(b => b.CheckInDate)
                    .Take(5)
                    .ToListAsync();

                ViewBag.CurrentBooking = currentBooking;
                ViewBag.BookingHistory = bookingHistory;
                ViewBag.CleaningDue = (room.LastCleaned == null ||
                    DateTime.Now.Subtract(room.LastCleaned.Value).TotalDays > 1);

                return View(room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading room details");
                TempData["ErrorMessage"] = "An error occurred while loading room details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCleaned(int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    return NotFound();
                }

                // Allow marking as cleaned if room is in an appropriate status
                if (room.Status == "Needs Cleaning" || room.Status == "Available" ||
                    room.Status == "Maintenance" || string.IsNullOrEmpty(room.Status))
                {
                    room.Status = "Available";
                    room.LastCleaned = DateTime.Now;

                    _context.Update(room);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Room {room.RoomNumber} has been marked as cleaned and is now available.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Room {room.RoomNumber} cannot be marked as cleaned because it is currently {room.Status}.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking room as cleaned");
                TempData["ErrorMessage"] = "An error occurred while updating room status: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmCleanRoom(int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Room = room;
                return View("ConfirmCleanRoom", room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET ConfirmCleanRoom");
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNeedsCleaning(int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    return NotFound();
                }

                // Only allow marking as needs cleaning if not occupied
                if (room.Status != "Occupied")
                {
                    room.Status = "Needs Cleaning";
                    _context.Update(room);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Room {room.RoomNumber} has been marked as needing cleaning.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Room {room.RoomNumber} is currently occupied and cannot be marked for cleaning.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking room as needs cleaning");
                TempData["ErrorMessage"] = "An error occurred while updating room status: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMaintenance(int id, string maintenanceNote = "", bool urgentIssue = false)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    return NotFound();
                }

                // Check if room is occupied
                var isOccupied = await _context.Bookings
                    .AnyAsync(b => b.RoomId == id && b.Status == "Checked-In");

                if (isOccupied)
                {
                    TempData["ErrorMessage"] = $"Room {room.RoomNumber} is currently occupied and cannot be marked for maintenance.";
                    return RedirectToAction(nameof(Index));
                }

                room.Status = "Maintenance";

                // Add urgency tag if needed
                if (urgentIssue && !string.IsNullOrEmpty(maintenanceNote))
                {
                    maintenanceNote = "[URGENT] " + maintenanceNote;
                }

                // Update or append new maintenance note
                if (!string.IsNullOrEmpty(maintenanceNote))
                {
                    string existingNotes = room.MaintenanceNotes ?? "";

                    // Add date timestamp to the note
                    string timestampedNote = $"[{DateTime.Now:MM/dd/yyyy HH:mm}] {maintenanceNote}";

                    if (string.IsNullOrEmpty(existingNotes))
                        room.MaintenanceNotes = timestampedNote;
                    else
                        room.MaintenanceNotes = timestampedNote + "\n\n" + existingNotes;
                }

                _context.Update(room);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room {room.RoomNumber} has been marked for maintenance.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking room for maintenance");
                TempData["ErrorMessage"] = "An error occurred while updating room status: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceNotes(int roomId, string maintenanceNotes)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    return NotFound();
                }

                room.MaintenanceNotes = maintenanceNotes;
                _context.Update(room);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Maintenance notes updated successfully.";
                return RedirectToAction(nameof(RoomDetails), new { id = roomId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating maintenance notes");
                TempData["ErrorMessage"] = "An error occurred while updating maintenance notes: " + ex.Message;
                return RedirectToAction(nameof(RoomDetails), new { id = roomId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomStatusCounts()
        {
            try
            {
                var rooms = await _context.Rooms.ToListAsync();

                // Handle null values
                foreach (var room in rooms)
                {
                    room.Status = room.Status ?? "Available";
                }

                var needsCleaning = rooms.Count(r => r.Status == "Needs Cleaning");
                var maintenance = rooms.Count(r => r.Status == "Maintenance");
                var available = rooms.Count(r => r.Status == "Available");
                var occupied = rooms.Count(r => r.Status == "Occupied");

                // Get recent checkouts (last 24 hours)
                var yesterday = DateTime.Now.AddDays(-1);
                var checkedOutBookings = await _context.Bookings
                    .CountAsync(b => b.CheckedOutAt >= yesterday);

                // Get rooms never cleaned or not cleaned in 3 days
                var threeDaysAgo = DateTime.Now.AddDays(-3);
                var needsAttention = rooms.Count(r =>
                    r.LastCleaned == null ||
                    (r.LastCleaned <= threeDaysAgo && r.Status == "Available"));

                return Json(new
                {
                    needsCleaning = needsCleaning,
                    maintenance = maintenance,
                    available = available,
                    occupied = occupied,
                    checkedOut = checkedOutBookings,
                    needsAttention = needsAttention
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching room status counts");
                return Json(new
                {
                    needsCleaning = 0,
                    maintenance = 0,
                    available = 0,
                    occupied = 0,
                    checkedOut = 0,
                    needsAttention = 0
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CleaningSchedule()
        {
            try
            {
                // Get all rooms that need cleaning
                var rooms = await _context.Rooms
                    .Where(r => r.Status == "Needs Cleaning")
                    .ToListAsync();

                // Get all active assignments from housekeeping staff
                var assignments = await _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Staff)
                    .Where(a => a.Status != "Completed")
                    .ToListAsync();

                // Create a dictionary to quickly look up assignments by room ID
                var roomAssignments = new Dictionary<int, HousekeepingAssignment>();
                foreach (var assignment in assignments)
                {
                    if (assignment.RoomId > 0) // Ensure valid roomId 
                    {
                        roomAssignments[assignment.RoomId] = assignment;
                    }
                }

                // Get housekeeping staff
                var housekeepingStaff = await _context.HousekeepingStaff
                    .Where(s => s.IsActive)
                    .ToListAsync() ?? new List<HousekeepingStaff>();

                ViewBag.HousekeepingStaff = housekeepingStaff;
                ViewBag.RoomAssignments = roomAssignments;

                // Get admin users for assignment tracking
                var adminUsers = await _context.Users
                    .Where(u => u.Role == "Admin" || u.Role == "FrontDesk")
                    .ToListAsync() ?? new List<User>();

                ViewBag.AdminUsers = adminUsers;

                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cleaning schedule");
                TempData["ErrorMessage"] = "An error occurred while loading cleaning schedule: " + ex.Message;

                // Initialize empty collections to prevent null references
                ViewBag.HousekeepingStaff = new List<HousekeepingStaff>();
                ViewBag.RoomAssignments = new Dictionary<int, HousekeepingAssignment>();
                ViewBag.AdminUsers = new List<User>();

                return View(new List<Room>());
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignCleaning(int roomId, int staffId)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                var staff = await _context.Users.FindAsync(staffId);

                if (room == null || staff == null)
                {
                    return NotFound();
                }

                // Here you would typically create an assignment record
                // For now, we'll just update the room status
                room.Status = "Needs Cleaning";

                // You could add an "AssignedTo" field to your Room model
                // room.AssignedTo = staffId;

                _context.Update(room);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room {room.RoomNumber} has been assigned to {staff.FirstName} {staff.LastName}.";
                return RedirectToAction(nameof(CleaningSchedule));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning cleaning task");
                TempData["ErrorMessage"] = "An error occurred while assigning the cleaning task: " + ex.Message;
                return RedirectToAction(nameof(CleaningSchedule));
            }
        }

        public async Task<IActionResult> RoomAssignments()
        {
            try
            {
                // Get all rooms
                var rooms = await _context.Rooms.ToListAsync();

                // Get all active assignments
                var assignments = await _context.HousekeepingAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Staff)
                    .Where(a => a.Status != "Completed")
                    .ToListAsync();

                // Create a dictionary to map roomId to staff name
                var roomAssignments = new Dictionary<int, string>();
                foreach (var assignment in assignments)
                {
                    if (assignment.Staff != null)
                    {
                        roomAssignments[assignment.RoomId] = assignment.Staff.FirstName + " " + assignment.Staff.LastName;
                    }
                }

                // Get housekeeping staff for dropdown lists
                var staffList = await _context.HousekeepingStaff
                    .Where(s => s.IsActive)
                    .Select(s => new {
                        StaffId = s.StaffId,
                        FullName = s.FirstName + " " + s.LastName
                    })
                    .ToListAsync();

                // Set counts for badge display in navigation
                ViewBag.PendingRoomAssignments = rooms.Count(r => r.Status == "Needs Cleaning");
                ViewBag.MaintenanceRequestsCount = rooms.Count(r => r.Status == "Maintenance");

                ViewBag.StaffList = staffList;
                ViewBag.RoomAssignments = roomAssignments;

                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading room assignments");
                TempData["ErrorMessage"] = "An error occurred while loading room assignments: " + ex.Message;

                // Set default values for navigation badges on error
                ViewBag.PendingRoomAssignments = 0;
                ViewBag.MaintenanceRequestsCount = 0;

                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> MaintenanceRequests()
        {
            try
            {
                // Get rooms in maintenance status
                var maintenanceRooms = await _context.Rooms
                    .Where(r => r.Status == "Maintenance")
                    .ToListAsync();

                // Create a list to hold maintenance request objects
                var maintenanceRequests = new List<dynamic>();

                foreach (var room in maintenanceRooms)
                {
                    // Parse maintenance notes to get requests
                    var notes = string.IsNullOrEmpty(room.MaintenanceNotes)
                        ? new List<string>()
                        : room.MaintenanceNotes.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    // Get the most recent note
                    var latestNote = notes.FirstOrDefault() ?? "";

                    // Extract timestamp if available
                    DateTime requestDate = DateTime.Now;
                    if (latestNote.StartsWith("[") && latestNote.Contains("]"))
                    {
                        var datePart = latestNote.Substring(1, latestNote.IndexOf("]") - 1);
                        if (DateTime.TryParse(datePart, out DateTime parsedDate))
                        {
                            requestDate = parsedDate;
                        }
                    }

                    // Create a dynamic object with request details
                    maintenanceRequests.Add(new
                    {
                        Room = room,
                        Description = latestNote,
                        RequestDate = requestDate,
                        Status = "Pending" // Default status
                    });
                }

                return View(maintenanceRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance requests");
                TempData["ErrorMessage"] = "An error occurred while loading maintenance requests: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
        public async Task<IActionResult> Inventory()
        {
            try
            {
                // Set counts for badge display in navigation
                ViewBag.PendingRoomAssignments = await _context.Rooms.CountAsync(r => r.Status == "Needs Cleaning");
                ViewBag.MaintenanceRequestsCount = await _context.Rooms.CountAsync(r => r.Status == "Maintenance");

                // Get inventory items with careful error handling
                List<InventoryItem> items;
                try
                {
                    items = await _context.InventoryItems.ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error retrieving inventory items");
                    TempData["ErrorMessage"] = "Unable to retrieve inventory items from the database.";

                    // Set default values
                    ViewBag.InventoryItems = new List<dynamic>();
                    ViewBag.TotalItems = 0;
                    ViewBag.WellStockedCount = 0;
                    ViewBag.LowStockCount = 0;
                    ViewBag.OutOfStockCount = 0;
                    ViewBag.PendingRoomAssignments = 0;
                    ViewBag.MaintenanceRequestsCount = 0;

                    return View();
                }

                var displayItems = new List<InventoryItem>();
                int wellStockedCount = 0;
                int lowStockCount = 0;
                int outOfStockCount = 0;

                // Process and count items
                foreach (var item in items)
                {
                    if (item.Available <= 0)
                        outOfStockCount++;
                    else if (item.Available <= item.ReorderLevel)
                        lowStockCount++;
                    else
                        wellStockedCount++;

                    displayItems.Add(item);
                }

                // Set ViewBag values
                ViewBag.InventoryItems = displayItems;
                ViewBag.TotalItems = items.Count;
                ViewBag.WellStockedCount = wellStockedCount;
                ViewBag.LowStockCount = lowStockCount;
                ViewBag.OutOfStockCount = outOfStockCount;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory");
                TempData["ErrorMessage"] = "An error occurred while loading inventory: " + ex.Message;

                // Set default values
                ViewBag.InventoryItems = new List<dynamic>();
                ViewBag.TotalItems = 0;
                ViewBag.WellStockedCount = 0;
                ViewBag.LowStockCount = 0;
                ViewBag.OutOfStockCount = 0;
                ViewBag.PendingRoomAssignments = 0;
                ViewBag.MaintenanceRequestsCount = 0;

                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueItem(int itemId, int quantity, string issuedTo, string roomNumber, string notes)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(itemId);

                if (item == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction(nameof(Inventory));
                }

                int available = item.TotalStock - item.InUse;

                if (quantity <= 0)
                {
                    TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                    return RedirectToAction(nameof(Inventory));
                }

                if (quantity > available)
                {
                    TempData["ErrorMessage"] = $"Cannot issue {quantity} items. Only {available} available.";
                    return RedirectToAction(nameof(Inventory));
                }

                // Update the item
                item.InUse += quantity;
                item.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully issued {quantity} {item.ItemName}(s).";
                return RedirectToAction(nameof(Inventory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing item");
                TempData["ErrorMessage"] = "An error occurred while issuing items: " + ex.Message;
                return RedirectToAction(nameof(Inventory));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveStock(int itemId, int quantity, string receiptNumber, string supplier, string notes)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(itemId);

                if (item == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction(nameof(Inventory));
                }

                if (quantity <= 0)
                {
                    TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                    return RedirectToAction(nameof(Inventory));
                }

                // Update the item
                item.TotalStock += quantity;
                item.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully received {quantity} {item.ItemName}(s).";
                return RedirectToAction(nameof(Inventory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving stock");
                TempData["ErrorMessage"] = "An error occurred while receiving stock: " + ex.Message;
                return RedirectToAction(nameof(Inventory));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveStock(int itemId, int quantity)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(itemId);
                if (item == null)
                {
                    return NotFound();
                }

                if (quantity > 0)
                {
                    item.TotalStock += quantity;
                    item.LastUpdated = DateTime.Now;

                    _context.Update(item);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Successfully received {quantity} {item.ItemName}(s).";
                }
                else
                {
                    TempData["ErrorMessage"] = "Quantity must be greater than zero.";
                }

                return RedirectToAction(nameof(Inventory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving stock");
                TempData["ErrorMessage"] = "An error occurred while receiving stock: " + ex.Message;
                return RedirectToAction(nameof(Inventory));
            }
        }

        [HttpPost]
        public IActionResult AddMaintenanceNote(int roomId, string maintenanceNote)
        {
            var room = _context.Rooms.Find(roomId);
            if (room != null)
            {
                room.Status = "Maintenance";
                room.MaintenanceNotes = (room.MaintenanceNotes ?? "") +
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm}] {maintenanceNote}";
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Room {room.RoomNumber} has been marked for maintenance.";
            }
            else
            {
                TempData["ErrorMessage"] = "Room not found.";
            }

            return RedirectToAction("Index");
        }


        // Handle bookings that checked out
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(int bookingId)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.Room)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId);

                if (booking == null)
                {
                    return NotFound();
                }

                // Update booking status
                booking.Status = "Checked-Out";
                booking.CheckedOutAt = DateTime.Now;

                // Update room status
                if (booking.Room != null)   
                {
                    booking.Room.Status = "Needs Cleaning";
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Checkout processed successfully. Room has been marked for cleaning.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing checkout");
                TempData["ErrorMessage"] = "An error occurred while processing checkout: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }


        private async Task SetCommonViewData()
        {
            try
            {
                // Count room assignments pending
                int roomAssignmentsCount = await _context.Rooms
                    .Where(r => r.Status == "Needs Cleaning")
                    .CountAsync();

                // Count maintenance requests
                int maintenanceRequestsCount = await _context.Rooms
                    .Where(r => r.Status == "Maintenance")
                    .CountAsync();

                // Set ViewBag values for navigation counts
                ViewBag.PendingRoomAssignments = roomAssignmentsCount;
                ViewBag.MaintenanceRequestsCount = maintenanceRequestsCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting common view data");
                ViewBag.PendingRoomAssignments = 0;
                ViewBag.MaintenanceRequestsCount = 0;
            }
        }
    }
}