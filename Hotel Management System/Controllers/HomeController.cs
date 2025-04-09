using System.Diagnostics;
using Hotel_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Hotel_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HotelManagementDbContext _context;

        public HomeController(ILogger<HomeController> logger, HotelManagementDbContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string? category = null, decimal? maxPrice = null)
        {
            ViewData["Title"] = "Dashboard";

            try
            {
                // First fix any null values in the database
                await FixNullValuesInDatabase();

                // Build the query
                var query = _context.Rooms.AsQueryable();

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(r => r.Category == category);
                    ViewBag.SelectedCategory = category;
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(r => r.PricePerNight <= maxPrice.Value);
                    ViewBag.SelectedMaxPrice = maxPrice;
                }

                // Get rooms using a safe approach
                List<Room> allRooms;
                try
                {
                    allRooms = await query.ToListAsync();

                    // Fix any nulls in memory
                    foreach (var room in allRooms)
                    {
                        room.Category = room.Category ?? "Standard";
                        room.Status = room.Status ?? "Available";
                        room.MaintenanceNotes = room.MaintenanceNotes ?? "";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading rooms");
                    allRooms = new List<Room>();
                }

                // Update room capacities if needed
                var roomsToUpdate = new List<Room>();
                foreach (var room in allRooms.Where(r => r.Capacity == 0))
                {
                    int capacity = 2; // Default

                    if (!string.IsNullOrEmpty(room.Category))
                    {
                        switch (room.Category.ToLower())
                        {
                            case "standard":
                                capacity = 2;
                                break;
                            case "deluxe":
                                capacity = 3;
                                break;
                            case "suite":
                                capacity = 4;
                                break;
                        }
                    }

                    // Update in memory
                    room.Capacity = capacity;

                    // Also update in database
                    var dbRoom = await _context.Rooms.FindAsync(room.RoomId);
                    if (dbRoom != null)
                    {
                        dbRoom.Capacity = capacity;
                        roomsToUpdate.Add(dbRoom);
                    }
                }

                // Save capacity updates
                if (roomsToUpdate.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Get categories for filter dropdown
                ViewBag.Categories = allRooms
                    .Select(r => r.Category ?? "Standard")
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .ToList();

                if (allRooms.Count == 0)
                {
                    ViewBag.NoRoomsMessage = "No available rooms match your criteria.";
                }

                return View(allRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rooms");
                ViewBag.ErrorMessage = "An error occurred while retrieving rooms.";
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

        // Only authorized users can access the Privacy page
        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home"); // Redirect to home page instead of login
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Only authorized users can create bookings
        [Authorize]
        public IActionResult Create()
        {
            return RedirectToAction("Create", "Booking");
        }
    }
}