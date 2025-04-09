using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Hotel_Management_System.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Hotel_Management_System.Helpers;
using System;
using Microsoft.AspNetCore.Authorization;

namespace Hotel_Management_System.Controllers
{
    public class AccessController : Controller
    {
        private readonly HotelManagementDbContext _context;
        private readonly ILogger<AccessController> _logger;

        public AccessController(HotelManagementDbContext context, ILogger<AccessController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = "")
        {
            if (HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(VMLogin modelLogin, string? returnUrl = "")
        {
            if (ModelState.IsValid)
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == modelLogin.Email);

                if (user != null)
                {
                    // Validate password
                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(modelLogin.Password, user.PasswordHash);

                    if (isPasswordValid)
                    {
                        var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Email),
                    new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var authProperties = new AuthenticationProperties
                        {
                            AllowRefresh = true,
                            IsPersistent = modelLogin.RememberMe
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        // If returnUrl is provided and it's a local URL, redirect to it
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        // Otherwise, redirect based on role
                        return user.Role switch
                        {
                            "Admin" => RedirectToAction("Dashboard", "Admin"),
                            "FrontDesk" => RedirectToAction("Dashboard", "FrontDesk"),
                            "Housekeeping" => RedirectToAction("Dashboard", "Housekeeping"),
                            _ => RedirectToAction("Index", "Home")
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Invalid password for user: {Email}", modelLogin.Email);
                        ViewData["ValidateMessage"] = "Invalid email or password.";
                    }
                }
                else
                {
                    _logger.LogWarning("User not found: {Email}", modelLogin.Email);
                    ViewData["ValidateMessage"] = "Invalid email or password.";
                }
            }

            return View(modelLogin);
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult Register(VMRegister model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    return View(model);
                }

                // Hash the password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                // Check the number of admins in the database
                int adminCount = _context.Users.Count(u => u.Role == "Admin");

                // Limit to 4 Admins; others become Guests
                string assignedRole = (adminCount < 4) ? "Admin" : "Guest";

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PasswordHash = hashedPassword, // Store hashed password
                    Role = assignedRole,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return RedirectToAction("Login");
            }
            return View(model);
        }

        // POST: Access/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
