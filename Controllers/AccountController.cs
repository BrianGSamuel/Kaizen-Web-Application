using KaizenWebApp.Data;
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
            // Skip this check for logout-related actions to allow normal logout
            var currentAction = ControllerContext.RouteData.Values["action"]?.ToString();
            if (currentAction == "Logout" || currentAction == "Login")
            {
                return false;
            }

            // Skip this check for logout-related requests
            var referrer = Request.Headers["Referer"].ToString();
            if (referrer.Contains("Logout") || referrer.Contains("logout"))
            {
                return false;
            }

            // Check if this is a direct URL access (no referrer or external referrer)
            var isDirectAccess = string.IsNullOrEmpty(referrer) || 
                                !referrer.Contains(Request.Host.Value) ||
                                referrer.Contains("newtab") ||
                                referrer.Contains("new-window");

            // Only end session for very obvious direct access cases
            // Be more lenient to allow normal navigation
            if (isDirectAccess && User.Identity?.IsAuthenticated == true && 
                string.IsNullOrEmpty(referrer) && Request.Method == "GET")
            {
                // Only end session for GET requests with no referrer at all
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
                new Claim("DepartmentName", user.DepartmentName),
                new Claim("Role", user.Role)
            };

            Console.WriteLine($"Creating claims - Name: {user.UserName}, Department: {user.DepartmentName}");

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Add persistent login success message
            TempData["LoginSuccessMessage"] = $"Welcome back, {user.EmployeeName}! You have successfully logged in.";

            // Check user role to determine navigation
            Console.WriteLine($"User logged in: {user.UserName} with role: {user.Role}, redirecting...");
            switch (user.Role.ToLower())
            {
                case "admin":
                    Console.WriteLine($"Redirecting admin user to Dashboard");
                    return RedirectToAction("Dashboard", "Admin");
                case "kaizenteam":
                    Console.WriteLine($"Redirecting kaizen team user to KaizenTeamDashboard");
                    return RedirectToAction("KaizenTeamDashboard", "Kaizen");
                case "supervisor":
                    Console.WriteLine($"Redirecting supervisor user to SupervisorDashboard");
                    return RedirectToAction("SupervisorDashboard", "Kaizen");
                case "manager":
                    return RedirectToAction("ManagerDashboard", "Kaizen");
                case "user":
                    return RedirectToAction("UserDashboard", "Kaizen");
                default:
                    return RedirectToAction("EngineerDashboard", "Kaizen");
            }
        }

        // ------------------- ADMIN REGISTER -------------------

        [Authorize]
        public IActionResult RegisterAdmin()
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            var role = User.FindFirst("Role")?.Value;
            
            if (username?.ToLower() != "admin" && role != "KaizenTeam")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            return View("RegisterAdmin");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterAdmin(RegisterViewModel model)
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            var role = User.FindFirst("Role")?.Value;
            
            if (username?.ToLower() != "admin" && role != "KaizenTeam")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Additional validation for Engineer and Manager roles
            if ((model.Role == "Engineer" || model.Role == "Manager") && string.IsNullOrEmpty(model.Email))
            {
                ModelState.AddModelError("Email", "Email is required for Engineer and Manager roles.");
            }
            
            // Clear email validation errors for non-Engineer/Manager roles
            if (model.Role != "Engineer" && model.Role != "Manager")
            {
                ModelState.Remove("Email");
            }

            // Handle KaizenTeam role - clear validation errors for fields that are hidden
            if (model.Role == "KaizenTeam")
            {
                ModelState.Remove("EmployeeName");
                ModelState.Remove("EmployeeNumber");
                ModelState.Remove("Department");
            }

            if (!ModelState.IsValid)
                return View("RegisterAdmin", model);

            Console.WriteLine($"Admin registration attempt - Username: {model.Username}, Employee Name: {model.EmployeeName}, Employee Number: {model.EmployeeNumber}, Department: {model.Department}, Plant: {model.Plant}, Role: {model.Role}, Email: {model.Email}");

            if (_context.Users.Any(u => u.UserName == model.Username))
            {
                ModelState.AddModelError("", "Username already exists.");
                return View("RegisterAdmin", model);
            }

            var user = new Users
            {
                UserName = model.Username,
                EmployeeName = model.Role == "KaizenTeam" ? "Kaizen Team" : model.EmployeeName,
                EmployeeNumber = model.Role == "KaizenTeam" ? "KAIZEN" : model.EmployeeNumber,
                DepartmentName = model.Role == "KaizenTeam" ? "Kaizen Department" : model.Department,
                Plant = model.Plant,
                Password = model.Password, // ⚠ Store securely using hashing in production
                Role = model.Role,
                Email = model.Email
            };

            Console.WriteLine($"Creating user - Username: {user.UserName}, Employee Name: {user.EmployeeName}, Employee Number: {user.EmployeeNumber}, Department: {user.DepartmentName}, Plant: {user.Plant}, Role: {user.Role}");

            _context.Users.Add(user);
            _context.SaveChanges();

            Console.WriteLine($"User saved with ID: {user.Id}");

            return RedirectToAction("Dashboard", "Admin");
        }

        // ------------------- SUPERVISOR REGISTRATION -------------------

        [Authorize]
        public IActionResult RegisterUser()
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.UserName == username);
            if (user?.Role?.ToLower() != "supervisor")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var model = new RegisterViewModel
            {
                Role = "User" // Supervisors can only register regular users
            };

            return View("RegisterUser", model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterUser(RegisterViewModel model)
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.UserName == username);
            if (user?.Role?.ToLower() != "supervisor")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            if (!ModelState.IsValid)
                return View("RegisterUser", model);

            // Auto-generate username if not provided
            if (string.IsNullOrEmpty(model.Username) && !string.IsNullOrEmpty(model.EmployeeNumber))
            {
                model.Username = model.EmployeeNumber + "-User";
            }

            // Force role to be "User" for supervisor registrations
            model.Role = "User";

            Console.WriteLine($"Supervisor registration attempt - Username: {model.Username}, Employee Number: {model.EmployeeNumber}, Department: {model.Department}, Plant: {model.Plant}, Role: {model.Role}");

            if (_context.Users.Any(u => u.UserName == model.Username))
            {
                ModelState.AddModelError("", "Username already exists.");
                return View("RegisterUser", model);
            }

            var newUser = new Users
            {
                UserName = model.Username,
                EmployeeName = model.EmployeeName,
                EmployeeNumber = model.EmployeeNumber,
                DepartmentName = model.Department,
                Plant = model.Plant,
                Password = model.Password, // ⚠ Store securely using hashing in production
                Role = model.Role
            };

            Console.WriteLine($"Creating user - Username: {newUser.UserName}, Department: {newUser.DepartmentName}, Plant: {newUser.Plant}, Role: {newUser.Role}");

            _context.Users.Add(newUser);
            _context.SaveChanges();

            Console.WriteLine($"User saved with ID: {newUser.Id}");

            TempData["SubmissionSuccessMessage"] = "User registered successfully!";
            return RedirectToAction("SupervisorDashboard", "Kaizen");
        }

        // ------------------- LOGOUT -------------------

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LogoutGet()
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
            TempData["SubmissionSuccessMessage"] = "Access Granted: You can change your password.";
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

            TempData["SubmissionSuccessMessage"] = "Password changed successfully!";
            
            // Redirect based on user role
            if (username.ToLower().Contains("user"))
            {
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            else if (username.ToLower().Contains("kaizenteam"))
            {
                return RedirectToAction("KaizenTeamDashboard", "Kaizen");
            }
            else if (username.ToLower().Contains("manager"))
            {
                return RedirectToAction("KaizenListManager", "Kaizen");
            }
            else if (username.ToLower().Contains("engineer"))
            {
                return RedirectToAction("EngineerDashboard", "Kaizen");
            }
            else
            {
                return RedirectToAction("EngineerDashboard", "Kaizen"); // Default fallback
            }
        }

        // ------------------- CHANGE PASSWORD FOR MANAGERS -------------------

        [Authorize]
        public async Task<IActionResult> ChangeManagerPassword()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow managers (users with "manager" in their username)
            if (username == null || !username.ToLower().Contains("manager"))
            {
                TempData["AlertMessage"] = "Access Denied: Only managers can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            return View("ChangeManagerPassword", new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeManagerPassword(ChangePasswordViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow managers (users with "manager" in their username)
            if (username == null || !username.ToLower().Contains("manager"))
            {
                TempData["AlertMessage"] = "Access Denied: Only managers can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            if (!ModelState.IsValid)
                return View("ChangeManagerPassword", model);

            if (username != model.Username)
            {
                ModelState.AddModelError("", "You can only change your own password.");
                return View("ChangeManagerPassword", model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View("ChangeManagerPassword", model);
            }

            // Verify current password
            if (user.Password != model.CurrentPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View("ChangeManagerPassword", model);
            }

            // Update password
            user.Password = model.NewPassword; // ⚠ Use hashing in production
            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("KaizenListManager", "Kaizen");
        }



        // ------------------- CHANGE PASSWORD FOR ENGINEERS -------------------

        [Authorize]
        public async Task<IActionResult> ChangeEngineerPassword()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow engineers (users without "user", "manager", "admin", "kaizenteam" in their username)
            if (username == null || 
                username.ToLower().Contains("user") || 
                username.ToLower().Contains("manager") || 
                username.ToLower().Contains("admin") || 
                username.ToLower().Contains("kaizenteam"))
            {
                TempData["AlertMessage"] = "Access Denied: Only engineers can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            return View("ChangeEngineerPassword", new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEngineerPassword(ChangePasswordViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow engineers (users without "user", "manager", "admin", "kaizenteam" in their username)
            if (username == null || 
                username.ToLower().Contains("user") || 
                username.ToLower().Contains("manager") || 
                username.ToLower().Contains("admin") || 
                username.ToLower().Contains("kaizenteam"))
            {
                TempData["AlertMessage"] = "Access Denied: Only engineers can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            if (!ModelState.IsValid)
                return View("ChangeEngineerPassword", model);

            if (username != model.Username)
            {
                ModelState.AddModelError("", "You can only change your own password.");
                return View("ChangeEngineerPassword", model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View("ChangeEngineerPassword", model);
            }

            // Verify current password
            if (user.Password != model.CurrentPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View("ChangeEngineerPassword", model);
            }

            // Update password
            user.Password = model.NewPassword; // ⚠ Use hashing in production
            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("EngineerDashboard", "Kaizen");
        }

        // ------------------- CHANGE PASSWORD FOR USERS -------------------

        [Authorize]
        public async Task<IActionResult> ChangeUserPassword()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow users (users with "user" in their username)
            if (username == null || !username.ToLower().Contains("user"))
            {
                TempData["AlertMessage"] = "Access Denied: Only users can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            return View("ChangeUserPassword", new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserPassword(ChangePasswordViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow users (users with "user" in their username)
            if (username == null || !username.ToLower().Contains("user"))
            {
                TempData["AlertMessage"] = "Access Denied: Only users can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            if (!ModelState.IsValid)
                return View("ChangeUserPassword", model);

            if (username != model.Username)
            {
                ModelState.AddModelError("", "You can only change your own password.");
                return View("ChangeUserPassword", model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View("ChangeUserPassword", model);
            }

            // Verify current password
            if (user.Password != model.CurrentPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View("ChangeUserPassword", model);
            }

            // Update password
            user.Password = model.NewPassword; // ⚠ Use hashing in production
            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("Kaizenform", "Kaizen");
        }

        // ------------------- CHANGE PASSWORD FOR SUPERVISORS -------------------

        [Authorize]
        public async Task<IActionResult> ChangeSupervisorPassword()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow supervisors
            if (username == null || User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value?.ToLower() != "supervisor")
            {
                TempData["AlertMessage"] = "Access Denied: Only supervisors can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            return View("ChangeSupervisorPassword", new ChangePasswordViewModel { Username = username });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeSupervisorPassword(ChangePasswordViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            
            // Only allow supervisors
            if (username == null || User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value?.ToLower() != "supervisor")
            {
                TempData["AlertMessage"] = "Access Denied: Only supervisors can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }
            
            if (!ModelState.IsValid)
                return View("ChangeSupervisorPassword", model);

            if (username != model.Username)
            {
                ModelState.AddModelError("", "You can only change your own password.");
                return View("ChangeSupervisorPassword", model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserName == model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View("ChangeSupervisorPassword", model);
            }

            // Verify current password
            if (user.Password != model.CurrentPassword) // ⚠ Use hashing in production
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View("ChangeSupervisorPassword", model);
            }

            // Update password
            user.Password = model.NewPassword; // ⚠ Use hashing in production
            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("SupervisorDashboard", "Kaizen");
        }
    }
}

