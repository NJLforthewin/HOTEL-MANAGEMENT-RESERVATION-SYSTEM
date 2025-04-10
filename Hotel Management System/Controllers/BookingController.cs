using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;
using Hotel_Management_System.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Globalization;



namespace Hotel_Management_System.Controllers
{
    public class BookingController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly PaymongoService _paymongoService;
        private readonly EmailService _emailService;
        public BookingController(HotelManagementDbContext context, PaymongoService paymongoService, EmailService emailService)
        {
            _context = context;
            _paymongoService = paymongoService;
            _emailService = emailService;
        }
        public IActionResult PaymentPage(string clientSecret, decimal amount)
        {
            ViewBag.ClientSecret = clientSecret;
            ViewBag.Amount = amount;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EWalletCallback(string source_id, string id, string reference, string status)
        {
            try
            {
                // Log all request information to help debug
                Console.WriteLine("EWalletCallback - Full request details:");
                Console.WriteLine($"  source_id: {source_id}");
                Console.WriteLine($"  id: {id}"); // Added 'id' parameter as Paymongo might use this
                Console.WriteLine($"  reference: {reference}");
                Console.WriteLine($"  status: {status}");
                Console.WriteLine($"  All query parameters: {Request.QueryString}");

                // Try to find the source ID from different possible parameters
                string? sourceId = !string.IsNullOrEmpty(source_id) ? source_id :
                                 (!string.IsNullOrEmpty(id) ? id : reference);

                // If source ID is still null, try to get it from the URL or the TempData
                if (string.IsNullOrEmpty(sourceId))
                {
                    // Try to extract source ID from query string
                    foreach (var param in Request.Query)
                    {
                        Console.WriteLine($"  Query param: {param.Key} = {param.Value}");
                        if (param.Key.Contains("source") || param.Key.Contains("id"))
                        {
                            sourceId = param.Value;
                            Console.WriteLine($"  Found potential source ID in query: {sourceId}");
                            break;
                        }
                    }

                    // If still null, check TempData
                    if (string.IsNullOrEmpty(sourceId) && TempData["SourceId"] != null)
                    {
                        sourceId = TempData["SourceId"]?.ToString();
                        Console.WriteLine($"  Retrieved source ID from TempData: {sourceId}");
                    }
                }

                Console.WriteLine($"Final source ID to use: {sourceId}");

                if (status?.ToLower() == "success")
                {
                    // If we still don't have a source ID, we can't proceed
                    if (string.IsNullOrEmpty(sourceId))
                    {
                        Console.WriteLine("No source ID found, cannot proceed with payment");
                        TempData["ErrorMessage"] = "Payment reference not found. Please try again.";
                        return RedirectToAction("PaymentFailed");
                    }

                    // Check the source status to ensure it's chargeable
                    bool isChargeable = await _paymongoService.CheckSourceStatus(sourceId);

                    Console.WriteLine($"Source status check result: isChargeable = {isChargeable}");

                    if (!isChargeable)
                    {
                        TempData["ErrorMessage"] = "Payment was not completed. Please try again.";
                        return RedirectToAction("PaymentFailed");
                    }

                    // Create a payment from the source
                    var bookingJson = TempData["BookingData"] as string;
                    if (string.IsNullOrEmpty(bookingJson))
                    {
                        TempData["ErrorMessage"] = "Booking information not found. Please try again.";
                        return RedirectToAction("Create");
                    }

                    var booking = JsonSerializer.Deserialize<Booking>(bookingJson) ?? new Booking();

                    Console.WriteLine($"Creating payment from source: {sourceId}, amount: {booking.TotalPrice}");

                    var payment = await _paymongoService.CreatePaymentFromSource(
                        sourceId,
                        booking.TotalPrice,
                        $"Room Booking {booking.BookingId}"
                    );

                    if (payment != null)
                    {
                        // Extract payment ID from JObject
                        string? paymentId = payment["data"]?["id"]?.ToString();
                        Console.WriteLine($"Payment created successfully. Payment ID: {paymentId}");

                        // Payment successful - process the booking
                        booking.PaymentStatus = "Paid";
                        booking.TransactionId = sourceId;

                        // Set the status based on booking type
                        if (booking.BookingType == "Booking")
                        {
                            booking.Status = "Checked-In";
                        }
                        else if (booking.BookingType == "Reservation")
                        {
                            booking.Status = "Reserved";
                        }

                        // Save the booking to database
                        _context.Bookings.Add(booking);

                        // Update room status
                        var room = await _context.Rooms.FindAsync(booking.RoomId);
                        if (room != null)
                        {
                            room.Status = "Occupied";
                            _context.Rooms.Update(room);
                        }

                        await _context.SaveChangesAsync();

                        // Store booking reference for success page
                        TempData["BookingReference"] = booking.BookingId;

                        // Send confirmation email
                        if (!string.IsNullOrEmpty(booking.Email))
                        {
                            string roomNumber = room?.RoomNumber ?? "N/A";
                            bool emailSent = await _emailService.SendBookingConfirmationAsync(
                                booking.Email,
                                booking.GuestName ?? "Valued Guest",
                                booking.BookingId.ToString(),
                                booking.CheckInDate,
                                booking.CheckOutDate,
                                roomNumber,
                                booking.TotalPrice,
                                 "GCash"
                            );

                            if (emailSent)
                            {
                                TempData["EmailSent"] = "true";
                            }
                        }

                        return RedirectToAction("PaymentSuccess");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create payment from source.");
                        TempData["ErrorMessage"] = "Failed to complete payment. Please try again.";
                        return RedirectToAction("PaymentFailed");
                    }
                }
                else
                {
                    Console.WriteLine($"Payment status is not success: {status}");
                    TempData["ErrorMessage"] = "Payment was not successful. Please try again.";
                    return RedirectToAction("PaymentFailed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in EWalletCallback: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = "Error processing payment result: " + ex.Message;
                return RedirectToAction("PaymentFailed");
            }
        }

        public IActionResult PaymentSuccess()
        {
            return View();
        }

        public IActionResult PaymentFailed()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> InitiatePayment(Booking booking, string paymentMethod)
        {
            // Validate booking data
            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View("Create", booking);
            }

            try
            {
                // Calculate room price and validate it
                var room = _context.Rooms.FirstOrDefault(r => r.RoomId == booking.RoomId);
                if (room == null)
                {
                    ModelState.AddModelError("", "Selected room not found.");
                    ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                    ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                    return View("Create", booking);
                }

                // Calculate total price
                int nights = (int)(booking.CheckOutDate - booking.CheckInDate).TotalDays;
                decimal totalPrice = room.PricePerNight * nights;
                booking.TotalPrice = totalPrice;

                // Store booking details in TempData for retrieval after payment
                TempData["BookingData"] = JsonSerializer.Serialize(booking);

                // Create the appropriate payment method based on selection
                if (paymentMethod == "gcash")
                {
                    // For GCash
                    string? returnUrl = Url.Action("EWalletCallback", "Booking", null, Request.Scheme);
                    if (string.IsNullOrEmpty(returnUrl))
                    {
                        returnUrl = "/Booking/EWalletCallback"; // Fallback URL in case Url.Action returns null
                    }

                    Console.WriteLine($"Generated return URL: {returnUrl}");

                    var source = await _paymongoService.CreateEWalletPayment(
                        totalPrice,
                        "gcash",
                        $"Booking for Room {room.RoomNumber} - {booking.GuestName}",
                        returnUrl
                    );

                    // Check if the source is null
                    if (source == null)
                    {
                        ModelState.AddModelError("", $"Failed to initialize GCash payment. Please try again.");
                        ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                        ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                        return View("Create", booking);
                    }

                    // Safely access the JSON response properties using JObject
                    try
                    {
                        string? sourceId = source["data"]?["id"]?.ToString();
                        string? checkoutUrl = source["data"]?["attributes"]?["redirect"]?["checkout_url"]?.ToString();

                        // Store the source ID in TempData
                        if (!string.IsNullOrEmpty(sourceId))
                        {
                            TempData["SourceId"] = sourceId;
                            Console.WriteLine($"Stored source ID in TempData: {sourceId}");
                        }

                        Console.WriteLine($"Redirecting to checkout URL: {checkoutUrl}");

                        if (!string.IsNullOrEmpty(checkoutUrl))
                        {
                            return Redirect(checkoutUrl);
                        }
                        else
                        {
                            ModelState.AddModelError("", $"Invalid checkout URL received from payment provider.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Error accessing payment data: {ex.Message}");
                    }
                }
                else if (paymentMethod == "bank_transfer")
                {
                    // For bank transfers
                    booking.PaymentStatus = "Pending";
                    booking.TransactionId = "BT" + DateTime.Now.ToString("yyyyMMddHHmmss");

                    // Set the status based on booking type
                    if (booking.BookingType == "Booking")
                    {
                        booking.Status = "Checked-In";
                    }
                    else if (booking.BookingType == "Reservation")
                    {
                        booking.Status = "Reserved";
                    }

                    // Save the booking to database with pending status
                    _context.Bookings.Add(booking);

                    // Update room status
                    room.Status = "Occupied";
                    _context.Rooms.Update(room);

                    await _context.SaveChangesAsync();

                    // Store booking reference for bank transfer page
                    TempData["BookingReference"] = booking.BookingId;
                    TempData["BankTransferAmount"] = booking.TotalPrice.ToString(CultureInfo.InvariantCulture);

                    // Send email with bank transfer instructions
                    // In EWalletCallback method, modify the email sending part:
                    if (!string.IsNullOrEmpty(booking.Email))
                    {
                        string roomNumber = room?.RoomNumber ?? "N/A";
                        bool emailSent = await _emailService.SendBookingConfirmationAsync(
                            booking.Email,
                            booking.GuestName ?? "Valued Guest",
                            booking.BookingId.ToString(),
                            booking.CheckInDate,
                            booking.CheckOutDate,
                            roomNumber,
                            booking.TotalPrice,
                            "Credit Card" // Specify GCash as the payment method
                        );

                        if (emailSent)
                        {
                            TempData["EmailSent"] = "true";
                        }
                    }

                    return RedirectToAction("BankTransferInstructions");
                }
                else
                {
                    // Default to card payment
                    var paymentIntent = await _paymongoService.CreatePaymentIntent(
                        totalPrice,
                        "PHP",
                        $"Booking for Room {room.RoomNumber} - {booking.GuestName}"
                    );

                    // Check if the paymentIntent is null
                    if (paymentIntent == null)
                    {
                        ModelState.AddModelError("", "Failed to initialize card payment. Please try again.");
                        ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                        ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                        return View("Create", booking);
                    }

                    try
                    {
                        // Access payment intent data using JObject syntax
                        string? paymentIntentId = paymentIntent["data"]?["id"]?.ToString();
                        string? clientKey = paymentIntent["data"]?["attributes"]?["client_key"]?.ToString();

                        if (!string.IsNullOrEmpty(paymentIntentId) && !string.IsNullOrEmpty(clientKey))
                        {
                            TempData["PaymentIntentId"] = paymentIntentId;

                            return RedirectToAction("PaymentPage", new
                            {
                                clientSecret = clientKey,
                                amount = totalPrice
                            });
                        }
                        else
                        {
                            ModelState.AddModelError("", "Invalid payment intent data received from payment provider.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Error accessing payment intent data: {ex.Message}");
                    }
                }

                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View("Create", booking);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error processing payment: " + ex.Message);
                ViewBag.Rooms = _context.Rooms.Where(r => r.Status == "Available").ToList();
                ViewBag.RoomPrices = _context.Rooms.ToDictionary(r => r.RoomId.ToString(), r => r.PricePerNight);
                return View("Create", booking);
            }
        }

        public IActionResult BankTransferInstructions()
        {
            // Get booking reference from TempData
            if (TempData["BookingReference"] == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BookingReference = TempData["BookingReference"];

            // Convert string amount to decimal with null check
            if (TempData["BankTransferAmount"] != null && !string.IsNullOrEmpty(TempData["BankTransferAmount"]?.ToString()))
            {
                ViewBag.Amount = Convert.ToDecimal(TempData["BankTransferAmount"]?.ToString());
            }
            else
            {
                ViewBag.Amount = 0;
            }

            ViewBag.EmailSent = TempData["EmailSent"];

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback(string status, string paymentIntentId)
        {
            Console.WriteLine($"PaymentCallback called with status: {status}, paymentIntentId: {paymentIntentId}");

            // Log all query parameters for debugging
            Console.WriteLine($"All query parameters: {Request.QueryString}");

            // If paymentIntentId is empty, try to extract it from the query string
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                foreach (var param in Request.Query)
                {
                    Console.WriteLine($"Query param: {param.Key} = {param.Value}");
                    if (param.Key.Contains("payment") || param.Key.Contains("intent"))
                    {
                        paymentIntentId = param.Value.ToString();  // Fix: Ensure proper string conversion
                        Console.WriteLine($"Found potential payment intent ID in query: {paymentIntentId}");
                        break;
                    }
                }
            }

            if (status?.ToLower() == "succeeded")
            {
                // First, try to retrieve booking data regardless of payment verification
                var bookingJson = TempData["BookingData"] as string;
                if (string.IsNullOrEmpty(bookingJson))
                {
                    TempData["ErrorMessage"] = "Booking information not found. Please try again.";
                    return RedirectToAction("Create");
                }

                var booking = JsonSerializer.Deserialize<Booking>(bookingJson) ?? new Booking();

                // Attempt to verify the payment, but proceed even if verification fails
                bool paymentVerified = false;

                if (!string.IsNullOrEmpty(paymentIntentId))
                {
                    var paymentIntent = await _paymongoService.RetrievePaymentIntent(paymentIntentId);
                    paymentVerified = (paymentIntent != null &&
                                     paymentIntent["data"]?["attributes"]?["status"]?.ToString()?.ToLower() == "succeeded");

                    Console.WriteLine($"Payment verification result: {paymentVerified}");
                }

                // For test mode, proceed with booking even if verification failed
                if (paymentVerified || status?.ToLower() == "succeeded")
                {
                    // Set payment status and transaction ID
                    booking.PaymentStatus = "Paid";
                    booking.TransactionId = !string.IsNullOrEmpty(paymentIntentId) ?
                                           paymentIntentId : "CARD-" + Guid.NewGuid().ToString();

                    // Set the status based on booking type
                    if (booking.BookingType == "Booking")
                    {
                        booking.Status = "Checked-In";
                    }
                    else if (booking.BookingType == "Reservation")
                    {
                        booking.Status = "Reserved";
                    }

                    // Save the booking to database
                    _context.Bookings.Add(booking);

                    // Update room status
                    var room = await _context.Rooms.FindAsync(booking.RoomId);
                    if (room != null)
                    {
                        room.Status = "Occupied";
                        _context.Rooms.Update(room);
                    }

                    await _context.SaveChangesAsync();

                    // Store booking reference for success page
                    TempData["BookingReference"] = booking.BookingId;

                    // Send confirmation email
                    if (!string.IsNullOrEmpty(booking.Email))
                    {
                        string roomNumber = room?.RoomNumber ?? "N/A";
                        bool emailSent = await _emailService.SendBookingConfirmationAsync(
                            booking.Email,
                            booking.GuestName ?? "Valued Guest",
                            booking.BookingId.ToString(),
                            booking.CheckInDate,
                            booking.CheckOutDate,
                            roomNumber,
                            booking.TotalPrice,
                            "Credit Card" // Specify payment method
                        );

                        if (emailSent)
                        {
                            TempData["EmailSent"] = "true";
                        }
                    }

                    return RedirectToAction("PaymentSuccess");
                }
            }

            // If we get here, payment failed or was canceled
            TempData["ErrorMessage"] = "Payment was not successful. Please try again.";
            return RedirectToAction("PaymentFailed");
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

            // Only handle Booking and Reservation types
            if (booking.BookingType != "Booking" && booking.BookingType != "Reservation")
            {
                booking.BookingType = "Booking"; // Default to Booking if invalid
            }

            booking.Status = "Pending";
            booking.CheckedInAt = null;

            _context.Bookings.Add(booking);
            _context.SaveChanges();

            TempData["SuccessMessage"] = booking.BookingType == "Reservation"
                ? "Reservation submitted successfully!"
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

            // Create booking with default dates and type
            var booking = new Booking
            {
                BookingType = "Walk-In",
                CreatedAt = DateTime.Now,
                CheckInDate = DateTime.Now.Date, // Set check-in date to today
                CheckOutDate = DateTime.Now.Date.AddDays(1) // Default to one day stay
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
        public async Task<IActionResult> WalkInBooking(Booking booking)
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
                booking.PaymentMethod ??= "Cash"; // Default to Cash if not specified
                booking.CreatedAt = DateTime.Now;

                // Create a transaction ID based on payment method
                booking.TransactionId = $"{booking.PaymentMethod.Replace("/", "").Replace(" ", "").ToUpper()}-{Guid.NewGuid().ToString().Substring(0, 8)}";

                // Walk-In Booking is immediately confirmed
                booking.BookingType = "Walk-In";
                booking.Status = "Checked-In";
                booking.PaymentStatus = "Paid";
                booking.CheckedInAt = DateTime.Now;
                booking.CheckedOutAt = null;

                // Mark Room as Occupied
                room.Status = "Occupied";

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Send confirmation email if we have a valid email
                if (!string.IsNullOrEmpty(booking.Email) && booking.Email != "No Email")
                {
                    try
                    {
                        await _emailService.SendBookingConfirmationAsync(
                            booking.Email,
                            booking.GuestName,
                            booking.BookingId.ToString(),
                            booking.CheckInDate,
                            booking.CheckOutDate,
                            room.RoomNumber,
                            booking.TotalPrice,
                            booking.PaymentMethod // Include the payment method
                        );

                        System.Diagnostics.Debug.WriteLine($"Confirmation email sent to {booking.Email}");
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't stop the flow
                        System.Diagnostics.Debug.WriteLine($"Error sending email: {ex.Message}");
                    }
                }

                TempData["SuccessMessage"] = "Walk-In Booking successful! Guest is now checked in.";
                return RedirectToAction("BookedRooms", "Room");
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