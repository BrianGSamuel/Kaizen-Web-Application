﻿using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace KaizenWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // Method to check if request is direct URL access and end session if so
        private async Task<bool> CheckAndEndSessionIfDirectAccess()
        {
            // Check if this is a direct URL access (no referrer or external referrer)
            var referrer = Request.Headers["Referer"].ToString();
            var isDirectAccess = string.IsNullOrEmpty(referrer) || 
                                !referrer.Contains(Request.Host.Value) ||
                                referrer.Contains("newtab") ||
                                referrer.Contains("new-window");

            if (isDirectAccess && User.Identity?.IsAuthenticated == true)
            {
                // Immediately end the session
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return true; // Session was ended
            }
            
            return false; // Session was not ended
        }

        // ------------------- LOGIN -------------------

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            Console.WriteLine($"Login attempt for username: {model.Username}");
            
            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            Console.WriteLine($"User found: {user != null}");
            if (user != null)
            {
                Console.WriteLine($"User details - Username: {user.UserName}, Department: {user.DepartmentName}, ID: {user.Id}");
            }
            
            if (user == null || user.Password != model.Password) // ⚠ Insecure: Use hashing in production
            {
                ModelState.AddModelError("", "Username or password is incorrect.");
                return View(model);
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("DepartmentName", user.DepartmentName)
            };

            Console.WriteLine($"Creating claims - Name: {user.UserName}, Department: {user.DepartmentName}");

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Check if username contains "user" to determine navigation
            if (user.UserName.ToLower().Contains("user"))
            {
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            else
            {
                return RedirectToAction("KaizenListManager", "Kaizen");
            }
        }

        // ------------------- REGISTER -------------------

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            Console.WriteLine($"Registration attempt - Username: {model.Username}, Department Name: {model.Name}");

            if (_context.Users.Any(u => u.UserName == model.Username))
            {
                ModelState.AddModelError("", "Username already exists.");
                return View(model);
            }

            var user = new Users
            {
                UserName = model.Username,
                DepartmentName = model.Name,
                Password = model.Password // ⚠ Store securely using hashing in production
            };

            Console.WriteLine($"Creating user - Username: {user.UserName}, Department: {user.DepartmentName}");

            _context.Users.Add(user);
            _context.SaveChanges();

            Console.WriteLine($"User saved with ID: {user.Id}");

            return RedirectToAction("Login", "Account");
        }

        // ------------------- LOGOUT -------------------

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ------------------- PASSWORD RECOVERY -------------------

        [AllowAnonymous]
        public IActionResult VerifyEmail()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerifyEmail(VerifyUsernameViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View(model);
            }

            return RedirectToAction("ChangePassword", new { username = user.UserName });
        }

        [AllowAnonymous]
        public IActionResult ChangePassword(string username)
        {
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("VerifyEmail");

            return View(new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View(model);
            }

            // Compare old password
            if (user.Password != model.NewPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("", "Old password is incorrect.");
                return View(model);
            }

            user.Password = model.NewPassword;
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        // ------------------- CHANGE PASSWORD (LOGGED IN USER) -------------------

        [Authorize]
        public async Task<IActionResult> ChangeMyPassword()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Check if username contains "user" - if so, deny access
            if (username != null && username.ToLower().Contains("user"))
            {
                TempData["AlertMessage"] = "Access Denied: Users are not allowed to change password.";
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            
            // Access granted for usernames without "user"
            TempData["Success"] = "Access Granted: You can change your password.";
            return View(new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeMyPassword(ChangePasswordViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Check if username contains "user" - if so, deny access
            if (username != null && username.ToLower().Contains("user"))
            {
                TempData["AlertMessage"] = "Access Denied: Users are not allowed to change password.";
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            
            if (!ModelState.IsValid)
                return View(model);

            if (username != model.Username)
            {
                ModelState.AddModelError("", "You can only change your own password.");
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View(model);
            }

            // Verify current password
            if (user.Password != model.CurrentPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            // Update password
            user.Password = model.NewPassword; // ⚠ Use hashing in production
            _context.SaveChanges();

            TempData["Success"] = "Password changed successfully!";
            
            // Redirect based on user role
            if (username.ToLower().Contains("user"))
            {
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            else
            {
                return RedirectToAction("KaizenListManager", "Kaizen");
            }
        }
    }
}

