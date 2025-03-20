using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hotel_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller
    {
        private readonly HotelManagementDbContext _context;

        public AdminController(HotelManagementDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard()
        {
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
        [Authorize(Roles = "Admin")]
        public IActionResult MakeAllRoomsAvailable()
        {
            var rooms = _context.Rooms.ToList();
            foreach (var room in rooms)
            {
                room.Status = "Available";
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = "All rooms have been set to Available.";
            return RedirectToAction("Rooms", "Room");
        }


    }
}