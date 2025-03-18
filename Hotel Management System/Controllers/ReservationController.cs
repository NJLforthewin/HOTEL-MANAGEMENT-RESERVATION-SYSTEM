using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hotel_Management_System.Models;
using System.Linq;
using System;

namespace Hotel_Management_System.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly HotelManagementDbContext _context;

        public ReservationController(HotelManagementDbContext context)
        {
            _context = context;
        }

        // Show reservation form
        [HttpGet]
        public IActionResult Create(int? roomId)
        {
            var rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
            ViewBag.Rooms = rooms;
            ViewBag.SelectedRoomId = roomId;
            ViewBag.RoomPrices = rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Booking reservation)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(reservation);
            }

            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == reservation.RoomId);
            if (room == null)
            {
                ModelState.AddModelError("RoomId", "Invalid Room ID.");
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(reservation);
            }

            if (reservation.CheckInDate >= reservation.CheckOutDate)
            {
                ModelState.AddModelError("", "Check-in date must be before Check-out date.");
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View(reservation);
            }

            reservation.TotalPrice = (reservation.CheckOutDate - reservation.CheckInDate).Days * room.PricePerNight;
            reservation.Status = "Pending";
            reservation.BookingType = "Reservation"; // Automatically set to Reservation

            _context.Bookings.Add(reservation);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Reservation created successfully!";
            return RedirectToAction("Index", "Home");
        }
    }
}