using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using KaizenWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text;

namespace KaizenWebApp.Controllers
{
    [Authorize]
    public class KaizenController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IFileService _fileService;
        private readonly IEmailService _emailService;
        private readonly IKaizenService _kaizenService;

        public KaizenController(AppDbContext context, IWebHostEnvironment env, IFileService fileService, IEmailService emailService, IKaizenService kaizenService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _kaizenService = kaizenService ?? throw new ArgumentNullException(nameof(kaizenService));
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
            var referrer = Request.Headers.Referer.ToString();
            if (referrer.Contains("Logout") || referrer.Contains("logout"))
            {
                return false;
            }

            // Skip this check for internal navigation to prevent redirect loops
            if (referrer.Contains(Request.Host.Value))
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

        // Custom authorization method
        private bool IsUserRole()
        {
            var username = User?.Identity?.Name;
            return username?.ToLower().Contains("user") == true;
        }

        // Custom authorization method for manager role
        private bool IsManagerRole()
        {
            var username = User?.Identity?.Name;
            return username?.ToLower().Contains("manager") == true;
        }

        // Custom authorization method for engineer role
        private bool IsEngineerRole()
        {
            var username = User?.Identity?.Name;
            return username?.ToLower().Contains("engineer") == true;
        }

        // Custom authorization method for kaizen team role
        private bool IsKaizenTeamRole()
        {
            // First check the role claim (more reliable)
            var role = User?.FindFirst("Role")?.Value;
            if (!string.IsNullOrEmpty(role) && role.ToLower() == "kaizenteam")
                return true;
                
            // Fallback to username check for backward compatibility
            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return false;
                
            var usernameLower = username.ToLower();
            // Check for various Kaizen Team username patterns
            return usernameLower.Contains("kaizenteam") || 
                   usernameLower.Contains("kaizen team") || 
                   usernameLower.Contains("kaizenteam") ||
                   usernameLower.Contains("kaizen-team") ||
                   usernameLower == "kaizen_team";
        }

        // Custom authorization method for supervisor role
        private bool IsSupervisorRole()
        {
            var username = User?.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.UserName == username);
            return user?.Role?.ToLower() == "supervisor";
        }

        // Get current user's information
        private async Task<Users?> GetCurrentUserAsync()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        }

        // Get user information by employee number extracted from username
        private async Task<Users?> GetUserByEmployeeNumberFromUsernameAsync()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return null;

            // Extract employee number from username (format: EmployeeNumber-User)
            var employeeNumber = ExtractEmployeeNumberFromUsername(username);
            if (string.IsNullOrEmpty(employeeNumber))
                return null;

            Console.WriteLine($"Extracted employee number from username '{username}': {employeeNumber}");

            // Find user by employee number
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);
            
            if (user != null)
            {
                Console.WriteLine($"Found user with employee number {employeeNumber}: {user.EmployeeName}");
            }
            else
            {
                Console.WriteLine($"No user found with employee number {employeeNumber}");
            }

            return user;
        }

        // Extract employee number from username
        private string? ExtractEmployeeNumberFromUsername(string username)
        {
            // Username format: EmployeeNumber-User
            if (username.EndsWith("-User"))
            {
                var employeeNumber = username.Replace("-User", "");
                return employeeNumber;
            }
            
            // Also check for other patterns like EmployeeNumber-Engineer, EmployeeNumber-Manager, etc.
            var parts = username.Split('-');
            if (parts.Length >= 2)
            {
                return parts[0];
            }

            return null;
        }

        // Helper method to escape CSV values
        private string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            // If the value contains comma, quote, or newline, wrap it in quotes and escape internal quotes
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            
            return value;
        }

        // GET: /Kaizen/Kaizenform
        [HttpGet]
        public async Task<IActionResult> Kaizenform()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "user" in their username
            if (!IsUserRole())
            {
                // Check if user is a kaizen team member
                if (IsKaizenTeamRole())
                {
                    return RedirectToAction("KaizenTeam");
                }
                // Check if user is a manager
                else if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                // Check if user is an engineer
                else if (IsEngineerRole())
                {
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer");
                }
            }

            Console.WriteLine("=== KaizenController.Kaizenform() called ===");
            try
            {
                // Get user information by employee number extracted from username
                var userByEmployeeNumber = await GetUserByEmployeeNumberFromUsernameAsync();
                
                var viewModel = new KaizenFormViewModel
                {
                    KaizenNo = await GenerateKaizenNo(),
                    DateSubmitted = DateTime.Today,
                    CostSavingType = "NoCostSaving", // Set default value
                    Plant = await GetCurrentUserPlant() // Set plant from user
                };

                // Auto-populate employee information from user found by employee number
                if (userByEmployeeNumber != null)
                {
                    viewModel.EmployeeName = userByEmployeeNumber.EmployeeName ?? "";
                    viewModel.EmployeeNo = userByEmployeeNumber.EmployeeNumber ?? "";
                    viewModel.Department = userByEmployeeNumber.DepartmentName;
                    viewModel.Plant = userByEmployeeNumber.Plant;
                    viewModel.EmployeePhotoPath = userByEmployeeNumber.EmployeePhotoPath ?? "";
                    
                    Console.WriteLine($"Auto-populated Employee Name: {viewModel.EmployeeName}");
                    Console.WriteLine($"Auto-populated Employee Number: {viewModel.EmployeeNo}");
                    Console.WriteLine($"Auto-populated Department: {viewModel.Department}");
                    Console.WriteLine($"Auto-populated Plant: {viewModel.Plant}");
                    Console.WriteLine($"Auto-populated Employee Photo Path: {viewModel.EmployeePhotoPath}");
                }
                else
                {
                    Console.WriteLine("No user found by employee number - using fallback method");
                    // Fallback to current user method if employee number lookup fails
                    var currentUser = await GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        viewModel.EmployeeName = currentUser.EmployeeName ?? "";
                        viewModel.EmployeeNo = currentUser.EmployeeNumber ?? "";
                        viewModel.Department = currentUser.DepartmentName;
                        viewModel.Plant = currentUser.Plant;
                        viewModel.EmployeePhotoPath = currentUser.EmployeePhotoPath ?? "";
                        Console.WriteLine($"Fallback - Auto-populated Employee Name: {viewModel.EmployeeName}");
                        Console.WriteLine($"Fallback - Auto-populated Employee Number: {viewModel.EmployeeNo}");
                        Console.WriteLine($"Fallback - Auto-populated Department: {viewModel.Department}");
                        Console.WriteLine($"Fallback - Auto-populated Plant: {viewModel.Plant}");
                        Console.WriteLine($"Fallback - Auto-populated Employee Photo Path: {viewModel.EmployeePhotoPath}");
                    }
                }



                // Debug information
                Console.WriteLine($"User authenticated: {User.Identity?.IsAuthenticated}");
                Console.WriteLine($"User name: {User.Identity?.Name}");
                Console.WriteLine($"User claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}"))}");

                return View("~/Views/Home/Kaizenform.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Kaizenform action: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Return a basic view model if there's an error
                var fallbackViewModel = new KaizenFormViewModel
                {
                    KaizenNo = await GenerateKaizenNo(),
                    DateSubmitted = DateTime.Today,
                    Department = null,
                    Plant = await GetCurrentUserPlant()
                };
                
                return View("~/Views/Home/Kaizenform.cshtml", fallbackViewModel);
            }
        }

        // POST: /Kaizen/Kaizenform
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kaizenform(KaizenFormViewModel viewModel)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "user" in their username
            if (!IsUserRole())
            {
                // Check if user is a manager
                if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                // Check if user is an engineer
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard");
                }
            }

            Console.WriteLine("=== POST Kaizenform action called ===");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            
            // Validate required fields explicitly
            bool hasValidationErrors = false;
            
            // Check required fields
            if (string.IsNullOrWhiteSpace(viewModel.EmployeeName))
            {
                ModelState.AddModelError("EmployeeName", "Employee Name is required.");
                hasValidationErrors = true;
            }
            
            if (string.IsNullOrWhiteSpace(viewModel.EmployeeNo))
            {
                ModelState.AddModelError("EmployeeNo", "Employee Number is required.");
                hasValidationErrors = true;
            }
            
            if (string.IsNullOrWhiteSpace(viewModel.SuggestionDescription))
            {
                ModelState.AddModelError("SuggestionDescription", "Suggestion Description is required.");
                hasValidationErrors = true;
            }
            
            // Note: Date Implemented, Employee Photo, Before Kaizen Image, After Kaizen Image, and Other Benefits
            // are now optional for editing existing records, so we don't validate them as required here
            
            // Check conditional required fields for cost saving
            if (viewModel.CostSavingType == "HasCostSaving")
            {
                if (!viewModel.CostSaving.HasValue || viewModel.CostSaving <= 0)
                {
                    ModelState.AddModelError("CostSaving", "Cost Saving amount is required when 'Has Cost Saving' is selected.");
                    hasValidationErrors = true;
                }
                
                if (!viewModel.DollarRate.HasValue || viewModel.DollarRate <= 0)
                {
                    ModelState.AddModelError("DollarRate", "Dollar Rate is required when 'Has Cost Saving' is selected.");
                    hasValidationErrors = true;
                }
            }
            
            // Debug: Log model state errors
            if (!ModelState.IsValid || hasValidationErrors)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                Console.WriteLine($"Model validation errors: {string.Join(", ", errors)}");
                
                // Log the viewModel properties
                Console.WriteLine($"KaizenNo: {viewModel.KaizenNo}");
                Console.WriteLine($"DateSubmitted: {viewModel.DateSubmitted}");
                Console.WriteLine($"Department: {viewModel.Department}");
                Console.WriteLine($"EmployeeName: {viewModel.EmployeeName}");
                Console.WriteLine($"EmployeeNo: {viewModel.EmployeeNo}");
                Console.WriteLine($"SuggestionDescription: {viewModel.SuggestionDescription}");
                Console.WriteLine($"CostSavingType: {viewModel.CostSavingType}");
                Console.WriteLine($"CostSaving: {viewModel.CostSaving}");
                Console.WriteLine($"DollarRate: {viewModel.DollarRate}");
                
                return View("~/Views/Home/Kaizenform.cshtml", viewModel);
            }

            try
            {
                Console.WriteLine("Starting form processing...");
                
                // Track which step has validation errors
                int errorStep = 1;
                bool hasImageError = false;
                
                // Validate file uploads and track which step they belong to
                if (viewModel.BeforeKaizenImage != null && !await IsValidImageAsync(viewModel.BeforeKaizenImage))
                {
                    ModelState.AddModelError("BeforeKaizenImage", "Invalid image format. Only PNG, JPG, JPEG, WebP files up to 5MB are allowed.");
                    errorStep = 3; // Step 3 contains BeforeKaizenImage
                    hasImageError = true;
                }

                if (viewModel.AfterKaizenImage != null && !await IsValidImageAsync(viewModel.AfterKaizenImage))
                {
                    ModelState.AddModelError("AfterKaizenImage", "Invalid image format. Only PNG, JPG, JPEG, WebP files up to 5MB are allowed.");
                    errorStep = 3; // Step 3 contains AfterKaizenImage
                    hasImageError = true;
                }



                // If there are image validation errors, return to the appropriate step
                if (hasImageError)
                {
                    TempData["ErrorStep"] = errorStep;
                    TempData["ImageValidationError"] = true;
                    return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                }

                // Get the user's department to ensure it's set correctly
                string userDepartment = await GetCurrentUserDepartment();

                // Ensure we have a department
                var finalDepartment = userDepartment ?? viewModel.Department?.Trim();
                if (string.IsNullOrEmpty(finalDepartment))
                {
                    ModelState.AddModelError("Department", "Department is required. Please ensure you are logged in with a valid department.");
                    return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                }

                var model = new KaizenForm
                {
                    KaizenNo = viewModel.KaizenNo ?? await GenerateKaizenNo(),
                    DateSubmitted = viewModel.DateSubmitted,
                    DateImplemented = viewModel.DateImplemented,
                    Department = finalDepartment,
                    Plant = viewModel.Plant ?? await GetCurrentUserPlant() ?? "KTY", // Default to KTY if not found
                    EmployeeName = viewModel.EmployeeName?.Trim(),
                    EmployeeNo = viewModel.EmployeeNo?.Trim(),
                    SuggestionDescription = viewModel.SuggestionDescription?.Trim(),

                    CostSaving = viewModel.CostSaving,
                    CostSavingType = viewModel.CostSavingType,
                    DollarRate = viewModel.DollarRate,
                    OtherBenefits = viewModel.OtherBenefits?.Trim()
                };

                // Handle file uploads for BeforeKaizenImage using FileService
                if (viewModel.BeforeKaizenImage != null && viewModel.BeforeKaizenImage.Length > 0)
                {
                    string beforeImagePath = await _fileService.SaveImageAsync(viewModel.BeforeKaizenImage, "uploads");
                    if (!string.IsNullOrEmpty(beforeImagePath))
                    {
                        model.BeforeKaizenImagePath = beforeImagePath;
                    }
                    else
                    {
                        ModelState.AddModelError("BeforeKaizenImage", "Failed to upload Before Kaizen image. Please try again.");
                        return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                    }
                }

                // Handle file uploads for AfterKaizenImage using FileService
                if (viewModel.AfterKaizenImage != null && viewModel.AfterKaizenImage.Length > 0)
                {
                    string afterImagePath = await _fileService.SaveImageAsync(viewModel.AfterKaizenImage, "uploads");
                    if (!string.IsNullOrEmpty(afterImagePath))
                    {
                        model.AfterKaizenImagePath = afterImagePath;
                    }
                    else
                    {
                        ModelState.AddModelError("AfterKaizenImage", "Failed to upload After Kaizen image. Please try again.");
                        return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                    }
                }

                // Handle EmployeePhoto - use existing photo from user profile only
                Console.WriteLine($"=== EMPLOYEE PHOTO DEBUG ===");
                Console.WriteLine($"viewModel.EmployeePhotoPath: '{viewModel.EmployeePhotoPath}'");
                
                if (!string.IsNullOrEmpty(viewModel.EmployeePhotoPath))
                {
                    // Use existing photo from user profile
                    model.EmployeePhotoPath = viewModel.EmployeePhotoPath;
                    Console.WriteLine($"Using existing employee photo: {model.EmployeePhotoPath}");
                }
                else
                {
                    // No photo available
                    ModelState.AddModelError("EmployeePhoto", "Employee photo is required. Please ensure you have a photo uploaded to your profile.");
                    return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                }
                
                Console.WriteLine($"Final model.EmployeePhotoPath: '{model.EmployeePhotoPath}'");
                Console.WriteLine($"=== END EMPLOYEE PHOTO DEBUG ===");

                Console.WriteLine("About to save to database...");
                Console.WriteLine($"Final model.EmployeePhotoPath before save: '{model.EmployeePhotoPath}'");
                _context.KaizenForms.Add(model);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Kaizen saved successfully with ID: {model.Id}");
                Console.WriteLine($"KaizenNo: {model.KaizenNo}");
                Console.WriteLine($"EmployeeName: {model.EmployeeName}");
                Console.WriteLine($"EmployeePhotoPath saved: '{model.EmployeePhotoPath}'");

                // Send email notification to engineer in the department
                await SendEmailNotificationToEngineer(model);

                TempData["SubmissionSuccessMessage"] = "Kaizen suggestion submitted successfully! Your submission has been received and is being processed.";
                return RedirectToAction("SuccessMessage", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while saving the Kaizen suggestion: {ex.Message} {ex.InnerException?.Message}");
                return View("~/Views/Home/Kaizenform.cshtml", viewModel);
            }
        }

        // GET: /Kaizen/Search (AJAX endpoint)
        [HttpGet]
        public async Task<IActionResult> Search(string searchString, string department, string status, string category, string startDate, string endDate)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var query = _context.KaizenForms.AsQueryable();
                var username = User?.Identity?.Name;
                var isUser = IsUserRole();
                var isManager = IsManagerRole();
                var userDepartment = await GetCurrentUserDepartment();

                Console.WriteLine($"=== SEARCH DEBUG ===");
                Console.WriteLine($"Search called by: {username}, IsUser: {isUser}, IsManager: {isManager}, UserDepartment: {userDepartment}");
                Console.WriteLine($"SearchString: '{searchString}', Department: '{department}', Status: '{status}', Category: '{category}', StartDate: '{startDate}', EndDate: '{endDate}'");
                
                // Debug: Check total kaizens in database
                var totalKaizens = await _context.KaizenForms.CountAsync();
                Console.WriteLine($"Total kaizens in database: {totalKaizens}");
                Console.WriteLine("Note: Cost saving is NOT used as a filter criterion");

                // Always filter by user's department (for both users and managers)
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered query by user department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return Json(new { success = true, kaizens = new List<object>() });
                }

                // For managers, filter to show only kaizens where engineer has approved and executive fields are filled
                if (isManager)
                {
                    // Debug: Check before EngineerStatus filter
                    var beforeEngineerStatusFilter = await query.CountAsync();
                    Console.WriteLine($"Search - Kaizens before EngineerStatus filter: {beforeEngineerStatusFilter}");
                    
                    // Filter to show only kaizens where engineer has approved
                    query = query.Where(k => k.EngineerStatus == "Approved");
                    Console.WriteLine("Filtered to show only kaizens where engineer has approved");
                    
                    // Debug: Check after EngineerStatus filter
                    var afterEngineerStatusFilter = await query.CountAsync();
                    Console.WriteLine($"Search - Kaizens after EngineerStatus filter: {afterEngineerStatusFilter}");
                    
                    // Filter to show only kaizens with executive filling data
                    query = query.Where(k => 
                        !string.IsNullOrEmpty(k.Category) &&
                        !string.IsNullOrEmpty(k.Comments) &&
                        !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                    );
                    Console.WriteLine("Filtered to show only kaizens with completed executive filling");
                    
                    // Debug: Check after executive filling filter
                    var afterExecutiveFillingFilter = await query.CountAsync();
                    Console.WriteLine($"Search - Kaizens after executive filling filter: {afterExecutiveFillingFilter}");
                }
                // For engineers, show all kaizens in their department (including pending ones)
                else if (IsEngineerRole())
                {
                    Console.WriteLine("Engineer search - showing all kaizens in department including pending ones");
                    // No additional filtering needed - engineers can see all kaizens in their department
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Department filter is now always applied based on user's department
                // No additional department filtering needed

                // Apply status filter based on user role
                if (!string.IsNullOrEmpty(status))
                {
                    if (isManager)
                    {
                        query = query.Where(k => (k.ManagerStatus ?? "Pending") == status);
                        Console.WriteLine($"Applied manager status filter: {status}");
                    }
                    else if (IsEngineerRole())
                    {
                        query = query.Where(k => (k.EngineerStatus ?? "Pending") == status);
                        Console.WriteLine($"Applied engineer status filter: {status}");
                    }
                }

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Debug: Check query before final execution
                var queryCount = await query.CountAsync();
                Console.WriteLine($"Query count before final execution: {queryCount}");
                
                var kaizens = await query
                    .OrderByDescending(k => k.DateSubmitted)
                    .Select(k => new
                    {
                        id = k.Id,
                        kaizenNo = k.KaizenNo,
                        dateSubmitted = k.DateSubmitted.ToString("yyyy-MM-dd"),
                        department = k.Department,
                        employeeName = k.EmployeeName,
                        employeeNo = k.EmployeeNo,
                        employeePhotoPath = k.EmployeePhotoPath,
                        costSaving = k.CostSaving,
                        // Engineer and Manager approval fields
                        engineerStatus = k.EngineerStatus ?? "Pending",
                        engineerApprovedBy = k.EngineerApprovedBy,
                        managerStatus = k.ManagerStatus ?? "Pending",
                        managerApprovedBy = k.ManagerApprovedBy,
                        // Executive filling fields
                        category = k.Category,
                        comments = k.Comments,
                        canImplementInOtherFields = k.CanImplementInOtherFields,
                        implementationArea = k.ImplementationArea,
                        // Manager comment fields
                        managerComments = k.ManagerComments,
                        managerSignature = k.ManagerSignature,
                        // Additional fields for popup
                        otherBenefits = k.OtherBenefits,
                        beforeKaizenImagePath = k.BeforeKaizenImagePath,
                        afterKaizenImagePath = k.AfterKaizenImagePath
                    })
                    .ToListAsync();

                Console.WriteLine($"Search returned {kaizens.Count} results (engineer approved with executive filling)");
                
                // Debug: Log the first few results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"Result: {k.kaizenNo}, {k.employeeName}, {k.department}");
                    Console.WriteLine($"  - EmployeePhotoPath: {k.employeePhotoPath}");
                    Console.WriteLine($"  - BeforeKaizenImagePath: {k.beforeKaizenImagePath}");
                    Console.WriteLine($"  - AfterKaizenImagePath: {k.afterKaizenImagePath}");
                    Console.WriteLine($"  - OtherBenefits: {k.otherBenefits}");

                }
                Console.WriteLine($"=== END SEARCH DEBUG ===");

                return Json(new { success = true, kaizens = kaizens });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/KaizenList
        [HttpGet]
        public async Task<IActionResult> KaizenList(string searchString, string department)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var query = _context.KaizenForms.AsQueryable();

                // For users with "user" in username, only filter by department when searching
                if (IsUserRole())
                {
                    var userDepartment = await GetCurrentUserDepartment();
                    Console.WriteLine($"User department: {userDepartment}");
                    
                    // Only filter by department when searching
                    if (!string.IsNullOrEmpty(searchString) && !string.IsNullOrEmpty(userDepartment))
                    {
                        query = query.Where(k => k.Department == userDepartment);
                        Console.WriteLine($"Filtered by department for search: {userDepartment}");
                    }
                    else
                    {
                        Console.WriteLine("User is not searching or no department found, showing all kaizens");
                    }
                }
                else
                {
                    Console.WriteLine("User is manager, showing all kaizens");
                }
                


                // Apply search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                }

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"Total kaizens found: {kaizens.Count}");
                
                // Debug: Show all kaizens in database
                var allKaizens = await _context.KaizenForms.ToListAsync();
                Console.WriteLine($"Total kaizens in database: {allKaizens.Count}");
                foreach (var k in allKaizens.Take(5))
                {
                    Console.WriteLine($"Kaizen: {k.KaizenNo}, Department: {k.Department}, Employee: {k.EmployeeName}");
                }

                // Get unique departments for filter
                ViewBag.Departments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                ViewBag.SearchString = searchString;
                ViewBag.Department = department;
                ViewBag.TotalCount = kaizens.Count;
                ViewBag.SearchTerm = searchString;

                return View("~/Views/Home/UserKaizenList.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while retrieving Kaizen suggestions: {ex.Message} {ex.InnerException?.Message}";
                return View("~/Views/Home/UserKaizenList.cshtml", new List<KaizenForm>());
            }
        }



        // GET: /Kaizen/TestDetails/{id} - Enhanced test endpoint to verify Details action
        [HttpGet]
        public async Task<IActionResult> TestDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can access test endpoints." });
            }

            try
            {
                Console.WriteLine($"=== TEST DETAILS FOR ID: {id} ===");
                
                var kaizen = await _context.KaizenForms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    Console.WriteLine("Kaizen not found");
                    return Json(new { success = false, message = "Kaizen not found" });
                }

                Console.WriteLine($"Found kaizen: {kaizen.KaizenNo}");
                Console.WriteLine($"EmployeeName: {kaizen.EmployeeName}");
                Console.WriteLine($"EmployeeNo: {kaizen.EmployeeNo}");
                Console.WriteLine($"Department: {kaizen.Department}");
                Console.WriteLine($"SuggestionDescription: {kaizen.SuggestionDescription}");
                Console.WriteLine($"OtherBenefits: {kaizen.OtherBenefits}");
                Console.WriteLine($"Category: {kaizen.Category}");
                Console.WriteLine($"Comments: {kaizen.Comments}");
                Console.WriteLine($"CanImplementInOtherFields: {kaizen.CanImplementInOtherFields}");
                Console.WriteLine($"ImplementationArea: {kaizen.ImplementationArea}");
                Console.WriteLine($"ManagerComments: {kaizen.ManagerComments}");
                Console.WriteLine($"ManagerSignature: {kaizen.ManagerSignature}");
                Console.WriteLine($"EmployeePhotoPath: {kaizen.EmployeePhotoPath}");
                Console.WriteLine($"BeforeKaizenImagePath: {kaizen.BeforeKaizenImagePath}");
                Console.WriteLine($"AfterKaizenImagePath: {kaizen.AfterKaizenImagePath}");
                Console.WriteLine($"CostSaving: {kaizen.CostSaving}");
                Console.WriteLine($"DollarRate: {kaizen.DollarRate}");
                Console.WriteLine($"CostSavingType: {kaizen.CostSavingType}");
                Console.WriteLine("=== END TEST DETAILS ===");

                return Json(new { 
                    success = true, 
                    message = "Test successful",
                    kaizen = new {
                        kaizen.Id,
                        kaizen.KaizenNo,
                        DateSubmitted = kaizen.DateSubmitted.ToString("yyyy-MM-dd"),
                        DateImplemented = kaizen.DateImplemented?.ToString("yyyy-MM-dd"),
                        kaizen.Department,
                        kaizen.EmployeeName,
                        kaizen.EmployeeNo,
                        kaizen.EmployeePhotoPath,
                        kaizen.SuggestionDescription,
                        kaizen.CostSaving,
                        kaizen.CostSavingType,
                        kaizen.DollarRate,
                        kaizen.OtherBenefits,
                        kaizen.BeforeKaizenImagePath,
                        kaizen.AfterKaizenImagePath,
                        kaizen.Category,
                        kaizen.Comments,
                        kaizen.CanImplementInOtherFields,
                        kaizen.ImplementationArea,
                        kaizen.ManagerComments,
                        kaizen.ManagerSignature
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test Details error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/TestDatabase - Simple endpoint to check database content
        [HttpGet]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                Console.WriteLine("=== TEST DATABASE ===");
                
                // Test database connection
                var totalKaizens = await _context.KaizenForms.CountAsync();
                Console.WriteLine($"Total kaizens in database: {totalKaizens}");
                
                var totalUsers = await _context.Users.CountAsync();
                Console.WriteLine($"Total users in database: {totalUsers}");
                
                // Get sample data
                var sampleKaizens = await _context.KaizenForms.Take(3).ToListAsync();
                Console.WriteLine("Sample kaizens:");
                foreach (var k in sampleKaizens)
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                }
                
                var sampleUsers = await _context.Users.Take(3).ToListAsync();
                Console.WriteLine("Sample users:");
                foreach (var u in sampleUsers)
                {
                    Console.WriteLine($"  - {u.UserName}: {u.DepartmentName}");
                }
                
                Console.WriteLine("=== END TEST DATABASE ===");
                
                return Json(new { 
                    success = true, 
                    totalKaizens = totalKaizens,
                    totalUsers = totalUsers,
                    sampleKaizens = sampleKaizens.Select(k => new { k.KaizenNo, k.EmployeeName, k.Department }),
                    sampleUsers = sampleUsers.Select(u => new { u.UserName, u.DepartmentName })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database test error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/TestUsers - Test endpoint to check users in database
        [HttpGet]
        public async Task<IActionResult> TestUsers()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can access test endpoints." });
            }

            try
            {
                var users = await _context.Users.ToListAsync();
                var userList = users.Select(u => new { 
                    id = u.Id, 
                    username = u.UserName, 
                    department = u.DepartmentName
                }).ToList();

                // Check if current user exists
                string currentUsername = User?.Identity?.Name;
                var currentUser = users.FirstOrDefault(u => u.UserName == currentUsername);

                // Check all kaizens
                var allKaizens = await _context.KaizenForms.ToListAsync();
                var kaizenList = allKaizens.Select(k => new {
                    id = k.Id,
                    kaizenNo = k.KaizenNo,
                    department = k.Department,
                    employeeName = k.EmployeeName
                }).ToList();

                return Json(new { 
                    success = true, 
                    totalUsers = users.Count,
                    totalKaizens = allKaizens.Count,
                    users = userList,
                    kaizens = kaizenList,
                    currentUsername = currentUsername,
                    currentUserFound = currentUser != null,
                    currentUserDepartment = currentUser?.DepartmentName
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/TestCurrentUser - Test endpoint to check current user's department
        [HttpGet]
        public async Task<IActionResult> TestCurrentUser()
        {
            try
            {
                Console.WriteLine("=== TEST CURRENT USER ===");
                
                var username = User?.Identity?.Name;
                var isUser = IsUserRole();
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"Current username: {username}");
                Console.WriteLine($"IsUser role: {isUser}");
                Console.WriteLine($"User department: {userDepartment}");
                
                // Get current user from database
                var currentUser = await _context.Users
                    .Where(u => u.UserName == username)
                    .FirstOrDefaultAsync();
                
                if (currentUser != null)
                {
                    Console.WriteLine($"User found in database: {currentUser.UserName}, Department: {currentUser.DepartmentName}");
                }
                else
                {
                    Console.WriteLine("User not found in database");
                }
                
                // Test search functionality
                var query = _context.KaizenForms.AsQueryable();
                if (isUser && !string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    var matchingKaizens = await query.CountAsync();
                    Console.WriteLine($"Kaizens matching user department: {matchingKaizens}");
                }
                
                var allKaizens = await _context.KaizenForms.CountAsync();
                var allUsers = await _context.Users.CountAsync();
                
                Console.WriteLine($"Total kaizens: {allKaizens}");
                Console.WriteLine($"Total users: {allUsers}");
                Console.WriteLine("=== END TEST CURRENT USER ===");
                
                return Json(new { 
                    success = true, 
                    username = username,
                    isUser = isUser,
                    userDepartment = userDepartment,
                    currentUser = currentUser != null ? new { currentUser.UserName, currentUser.DepartmentName } : null,
                    totalKaizens = allKaizens,
                    totalUsers = allUsers
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test current user error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/TestUserSearch - Test endpoint to debug user search issues
        [HttpGet]
        public async Task<IActionResult> TestUserSearch(string searchString)
        {
            try
            {
                Console.WriteLine("=== TEST USER SEARCH ===");
                Console.WriteLine($"Testing search for: {searchString}");
                
                var username = User?.Identity?.Name;
                var isUser = IsUserRole();
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"Current user: {username}, IsUser: {isUser}, Department: {userDepartment}");
                
                var query = _context.KaizenForms.AsQueryable();
                
                // Apply user department filter if user
                if (isUser && !string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by user department: {userDepartment}");
                }
                
                // Apply search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }
                
                var results = await query
                    .OrderByDescending(k => k.DateSubmitted)
                    .Select(k => new { k.Id, k.KaizenNo, k.EmployeeName, k.Department })
                    .ToListAsync();
                
                Console.WriteLine($"Found {results.Count} results");
                foreach (var r in results.Take(5))
                {
                    Console.WriteLine($"  - {r.KaizenNo}: {r.EmployeeName} ({r.Department})");
                }
                Console.WriteLine("=== END TEST USER SEARCH ===");
                
                return Json(new { 
                    success = true, 
                    searchString = searchString,
                    username = username,
                    isUser = isUser,
                    userDepartment = userDepartment,
                    results = results
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test user search error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Allow both users and managers to view details
            // Users can only view their own kaizens, managers can view all

            try
            {
                // Log the request
                Console.WriteLine($"Details request for ID: {id}");
                
                var kaizen = await _context.KaizenForms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    Console.WriteLine($"Kaizen not found for ID: {id}");
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                Console.WriteLine($"Kaizen found: {kaizen.KaizenNo}");

                // Create the response data
                var responseData = new
                {
                    kaizen.Id,
                    kaizen.KaizenNo,
                    DateSubmitted = kaizen.DateSubmitted.ToString("yyyy-MM-dd"),
                    DateImplemented = kaizen.DateImplemented?.ToString("yyyy-MM-dd"),
                    kaizen.Department,
                    kaizen.EmployeeName,
                    kaizen.EmployeeNo,
                    kaizen.EmployeePhotoPath,
                    kaizen.SuggestionDescription,
                    kaizen.CostSaving,
                    kaizen.CostSavingType,
                    kaizen.DollarRate,
                    kaizen.OtherBenefits,
                    kaizen.BeforeKaizenImagePath,
                    kaizen.AfterKaizenImagePath,
                    // Approval status fields
                    EngineerStatus = kaizen.EngineerStatus,
                    EngineerApprovedBy = kaizen.EngineerApprovedBy,
                    ManagerStatus = kaizen.ManagerStatus,
                    ManagerApprovedBy = kaizen.ManagerApprovedBy,
                    // Executive fill fields
                    Category = kaizen.Category,
                    Comments = kaizen.Comments,
                    CanImplementInOtherFields = kaizen.CanImplementInOtherFields,
                    ImplementationArea = kaizen.ImplementationArea,
                    // Manager comment fields
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature
                };

                // Debug logging
                Console.WriteLine($"Details - Kaizen ID: {kaizen.Id}");
                Console.WriteLine($"Details - EmployeePhotoPath: {kaizen.EmployeePhotoPath}");
                Console.WriteLine($"Details - BeforeKaizenImagePath: {kaizen.BeforeKaizenImagePath}");
                Console.WriteLine($"Details - AfterKaizenImagePath: {kaizen.AfterKaizenImagePath}");
                Console.WriteLine($"Details - OtherBenefits: {kaizen.OtherBenefits}");
                Console.WriteLine($"Details - OtherBenefits length: {kaizen.OtherBenefits?.Length ?? 0}");
                Console.WriteLine($"Details - OtherBenefits is null: {kaizen.OtherBenefits == null}");
                Console.WriteLine($"Details - OtherBenefits is empty: {string.IsNullOrEmpty(kaizen.OtherBenefits)}");

                Console.WriteLine("Returning JSON response");
                return Json(new { success = true, data = responseData });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Details action: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/GetDepartments - Get all departments for dropdown
        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                var departments = await _context.Users
                    .Where(u => !string.IsNullOrEmpty(u.DepartmentName))
                    .Select(u => u.DepartmentName)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                return Json(new { success = true, departments = departments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error retrieving departments: {ex.Message}" });
            }
        }

        // GET: /Kaizen/GetExecutiveFillData/{id}
        [HttpGet]
        public async Task<IActionResult> GetExecutiveFillData(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                var viewModel = new KaizenFormViewModel
                {
                    Id = kaizen.Id,
                    KaizenNo = kaizen.KaizenNo,
                    DateSubmitted = kaizen.DateSubmitted,
                    DateImplemented = kaizen.DateImplemented,
                    Department = kaizen.Department,
                    EmployeeName = kaizen.EmployeeName,
                    EmployeeNo = kaizen.EmployeeNo,
                    SuggestionDescription = kaizen.SuggestionDescription,
                    CostSaving = kaizen.CostSaving,
                    CostSavingType = kaizen.CostSavingType,
                    DollarRate = kaizen.DollarRate,
                    OtherBenefits = kaizen.OtherBenefits,
                    BeforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    AfterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    EmployeePhotoPath = kaizen.EmployeePhotoPath,
                    // New fields
                    Category = kaizen.Category,
                    Comments = kaizen.Comments,
                    CanImplementInOtherFields = kaizen.CanImplementInOtherFields,
                    ImplementationArea = kaizen.ImplementationArea,
                    // Manager comment fields
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature,
                    // Engineer and Manager status fields
                    EngineerStatus = kaizen.EngineerStatus,
                    EngineerApprovedBy = kaizen.EngineerApprovedBy,
                    ManagerStatus = kaizen.ManagerStatus,
                    ManagerApprovedBy = kaizen.ManagerApprovedBy
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Allow kaizen team, managers, and engineers (users without "user" in their username)
            if (IsUserRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                var viewModel = new KaizenFormViewModel
                {
                    Id = kaizen.Id,
                    KaizenNo = kaizen.KaizenNo,
                    DateSubmitted = kaizen.DateSubmitted,
                    DateImplemented = kaizen.DateImplemented,
                    Department = kaizen.Department,
                    EmployeeName = kaizen.EmployeeName,
                    EmployeeNo = kaizen.EmployeeNo,
                    SuggestionDescription = kaizen.SuggestionDescription,
                    CostSaving = kaizen.CostSaving,
                    CostSavingType = kaizen.CostSavingType,
                    DollarRate = kaizen.DollarRate,
                    OtherBenefits = kaizen.OtherBenefits,
                    BeforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    AfterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    EmployeePhotoPath = kaizen.EmployeePhotoPath,
                    // New fields
                    Category = kaizen.Category,
                    Comments = kaizen.Comments,
                    CanImplementInOtherFields = kaizen.CanImplementInOtherFields,
                    ImplementationArea = kaizen.ImplementationArea,
                    // Manager comment fields
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature,
                    // Engineer and Manager status fields
                    EngineerStatus = kaizen.EngineerStatus,
                    EngineerApprovedBy = kaizen.EngineerApprovedBy,
                    ManagerStatus = kaizen.ManagerStatus,
                    ManagerApprovedBy = kaizen.ManagerApprovedBy
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // POST: /Kaizen/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KaizenFormViewModel viewModel)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Allow kaizen team, managers, and engineers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers, engineers, and kaizen team can edit kaizens." });
            }

            try
            {
                // For editing existing records, we don't need to validate file uploads as required
                // since they might already have values. Only validate if new files are uploaded.
                var errors = new List<string>();
                
                // Check if this is an edit operation (Id > 0)
                bool isEditOperation = viewModel.Id > 0;
                
                // Debug logging
                Console.WriteLine($"Edit operation - Id: {viewModel.Id}, isEditOperation: {isEditOperation}");
                Console.WriteLine($"EmployeeName: {viewModel.EmployeeName}");
                Console.WriteLine($"EmployeeNo: {viewModel.EmployeeNo}");
                Console.WriteLine($"SuggestionDescription: {viewModel.SuggestionDescription}");
                Console.WriteLine($"EmployeePhoto: {(viewModel.EmployeePhoto != null ? "Has file" : "No file")}");
                Console.WriteLine($"BeforeKaizenImage: {(viewModel.BeforeKaizenImage != null ? "Has file" : "No file")}");
                Console.WriteLine($"AfterKaizenImage: {(viewModel.AfterKaizenImage != null ? "Has file" : "No file")}");
                Console.WriteLine($"OtherBenefits: {viewModel.OtherBenefits}");
                
                // Only validate required fields for new records, not for edits
                if (!isEditOperation)
                {
                    if (string.IsNullOrWhiteSpace(viewModel.EmployeeName))
                    {
                        errors.Add("Employee Name is required.");
                    }
                    
                    if (string.IsNullOrWhiteSpace(viewModel.EmployeeNo))
                    {
                        errors.Add("Employee Number is required.");
                    }
                    
                    if (string.IsNullOrWhiteSpace(viewModel.SuggestionDescription))
                    {
                        errors.Add("Suggestion Description is required.");
                    }
                    
                    if (viewModel.EmployeePhoto == null || viewModel.EmployeePhoto.Length == 0)
                    {
                        errors.Add("Employee Photo is required.");
                    }
                    
                    if (viewModel.BeforeKaizenImage == null || viewModel.BeforeKaizenImage.Length == 0)
                    {
                        errors.Add("Before Kaizen Image is required.");
                    }
                    
                    if (viewModel.AfterKaizenImage == null || viewModel.AfterKaizenImage.Length == 0)
                    {
                        errors.Add("After Kaizen Image is required.");
                    }
                    
                    if (string.IsNullOrWhiteSpace(viewModel.OtherBenefits))
                    {
                        errors.Add("Other Benefits is required.");
                    }
                }
                else
                {
                    Console.WriteLine("Skipping validation for edit operation");
                }
                
                if (errors.Any())
                {
                    Console.WriteLine($"Validation errors found: {string.Join(", ", errors)}");
                    return Json(new { success = false, message = "Validation errors: " + string.Join(", ", errors) });
                }
                
                // Also check ModelState for any automatic validation errors
                if (!ModelState.IsValid)
                {
                    var modelStateErrors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    
                    if (modelStateErrors.Any())
                    {
                        Console.WriteLine($"ModelState validation errors: {string.Join(", ", modelStateErrors)}");
                        return Json(new { success = false, message = "Validation errors: " + string.Join(", ", modelStateErrors) });
                    }
                }

                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Update the kaizen record
                kaizen.DateSubmitted = viewModel.DateSubmitted;
                kaizen.DateImplemented = viewModel.DateImplemented;
                kaizen.Department = viewModel.Department?.Trim();
                kaizen.EmployeeName = viewModel.EmployeeName?.Trim();
                kaizen.EmployeeNo = viewModel.EmployeeNo?.Trim();
                kaizen.SuggestionDescription = viewModel.SuggestionDescription?.Trim();
                kaizen.CostSaving = viewModel.CostSaving;
                kaizen.CostSavingType = viewModel.CostSavingType;
                kaizen.DollarRate = viewModel.DollarRate;
                kaizen.OtherBenefits = viewModel.OtherBenefits?.Trim();
                
                // Update Executive/Engineer fields
                kaizen.Category = viewModel.Category?.Trim();
                kaizen.Comments = viewModel.Comments?.Trim();
                kaizen.CanImplementInOtherFields = viewModel.CanImplementInOtherFields?.Trim();
                kaizen.ImplementationArea = viewModel.ImplementationArea?.Trim();
                
                // Update Manager comment fields
                kaizen.ManagerComments = viewModel.ManagerComments?.Trim();
                kaizen.ManagerSignature = viewModel.ManagerSignature?.Trim();

                // Handle file uploads for BeforeKaizenImage
                if (viewModel.BeforeKaizenImage != null && viewModel.BeforeKaizenImage.Length > 0)
                {
                    if (!IsValidImage(viewModel.BeforeKaizenImage))
                    {
                        return Json(new { success = false, message = "Invalid before image file. Only PNG, JPG, JPEG, WebP up to 5MB allowed." });
                    }

                    string beforeFileName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.BeforeKaizenImage.FileName)}";
                    string beforePath = Path.Combine("uploads", beforeFileName);
                    string fullBeforePath = Path.Combine(_env.WebRootPath, beforePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullBeforePath));
                    using (var stream = new FileStream(fullBeforePath, FileMode.Create))
                    {
                        await viewModel.BeforeKaizenImage.CopyToAsync(stream);
                    }
                    kaizen.BeforeKaizenImagePath = "/" + beforePath.Replace("\\", "/");
                }

                // Handle file uploads for AfterKaizenImage
                if (viewModel.AfterKaizenImage != null && viewModel.AfterKaizenImage.Length > 0)
                {
                    if (!IsValidImage(viewModel.AfterKaizenImage))
                    {
                        return Json(new { success = false, message = "Invalid after image file. Only PNG, JPG, JPEG, WebP up to 5MB allowed." });
                    }

                    string afterFileName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.AfterKaizenImage.FileName)}";
                    string afterPath = Path.Combine("uploads", afterFileName);
                    string fullAfterPath = Path.Combine(_env.WebRootPath, afterPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullAfterPath));
                    using (var stream = new FileStream(fullAfterPath, FileMode.Create))
                    {
                        await viewModel.AfterKaizenImage.CopyToAsync(stream);
                    }
                    kaizen.AfterKaizenImagePath = "/" + afterPath.Replace("\\", "/");
                }

                _context.KaizenForms.Update(kaizen);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Kaizen suggestion updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred while updating the Kaizen suggestion: {ex.Message}" });
            }
        }

        private async Task<string> GenerateKaizenNo()
        {
            // Get current user's plant
            var plant = await GetCurrentUserPlant();
            if (string.IsNullOrEmpty(plant))
            {
                plant = "KTY"; // Default plant if not found
            }

            // Get current year (last 2 digits)
            var year = DateTime.Now.Year.ToString()[2..]; // e.g., "25" for 2025

            // Get current quarter (3 months per quarter)
            var month = DateTime.Now.Month;
            var quarter = ((month - 1) / 3) + 1; // 1-3 = Q1, 4-6 = Q2, 7-9 = Q3, 10-12 = Q4

            // Get suggestion number for this quarter
            var quarterStart = new DateTime(DateTime.Now.Year, ((quarter - 1) * 3) + 1, 1);
            var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
            
            var suggestionCount = _context.KaizenForms.Count(k => 
                k.DateSubmitted >= quarterStart && 
                k.DateSubmitted <= quarterEnd) + 1;

            return $"{plant}-{year}-{quarter:D2}-{suggestionCount:D2}";
        }

        private bool IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > 5 * 1024 * 1024) return false; // Max 5MB
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        // Updated to use FileService for consistency
        private async Task<bool> IsValidImageAsync(IFormFile file)
        {
            return await _fileService.IsValidImageAsync(file);
        }



        private async Task<string> GetCurrentUserPlant()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var username = User.Identity.Name;
                    Console.WriteLine($"Getting plant for user: {username}");
                    
                    if (string.IsNullOrEmpty(username))
                    {
                        Console.WriteLine("Username is null or empty");
                        return null;
                    }
                    
                    var user = await _context.Users
                        .Where(u => u.UserName == username)
                        .FirstOrDefaultAsync();
                    
                    if (user != null)
                    {
                        Console.WriteLine($"Found user: {user.UserName}, Plant: {user.Plant}");
                        return user.Plant;
                    }
                    
                    Console.WriteLine($"No user found for username: {username}");
                    return null;
                }
                else
                {
                    Console.WriteLine("User is not authenticated");
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCurrentUserPlant: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetCurrentUserDepartment()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var username = User.Identity.Name;
                    Console.WriteLine($"Getting department for user: {username}");
                    
                    if (string.IsNullOrEmpty(username))
                    {
                        Console.WriteLine("Username is null or empty");
                        return null;
                    }
                    
                    // Direct query to get the department name for Potter123
                    var user = await _context.Users
                        .Where(u => u.UserName == username)
                        .FirstOrDefaultAsync();
                    
                    if (user != null)
                    {
                        Console.WriteLine($"Found user: {user.UserName}, Department: {user.DepartmentName}");
                        return user.DepartmentName;
                    }
                    
                    Console.WriteLine($"No user found for username: {username}");
                    
                    // Debug: Let's see all users to understand what's happening
                    var allUsers = await _context.Users.ToListAsync();
                    Console.WriteLine($"Total users in database: {allUsers.Count}");
                    foreach (var u in allUsers)
                    {
                        Console.WriteLine($"User: {u.UserName}, Department: {u.DepartmentName}");
                    }
                    
                    return null;
                }
                else
                {
                    Console.WriteLine("User is not authenticated");
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCurrentUserDepartment: {ex.Message}");
                return null;
            }
        }

        // Send email notification to engineer in the department
        private async Task SendEmailNotificationToEngineer(KaizenForm kaizenForm)
        {
            try
            {
                Console.WriteLine($"=== SENDING EMAIL NOTIFICATION ===");
                Console.WriteLine($"Kaizen No: {kaizenForm.KaizenNo}");
                Console.WriteLine($"Department: {kaizenForm.Department}");

                // Find engineer in the same department
                var engineer = await _context.Users
                    .Where(u => u.DepartmentName == kaizenForm.Department && 
                               u.Role.ToLower() == "engineer" && 
                               !string.IsNullOrEmpty(u.Email))
                    .FirstOrDefaultAsync();

                if (engineer == null)
                {
                    Console.WriteLine($"No engineer found in department: {kaizenForm.Department}");
                    return;
                }

                Console.WriteLine($"Found engineer: {engineer.EmployeeName} ({engineer.Email})");

                // Generate website URL
                var websiteUrl = $"{Request.Scheme}://{Request.Host}";
                Console.WriteLine($"Website URL: {websiteUrl}");

                // Find similar kaizen suggestions for the engineer's department
                var similarKaizens = await _kaizenService.GetSimilarKaizensAsync(
                    kaizenForm.SuggestionDescription,
                    kaizenForm.CostSavingType,
                    kaizenForm.OtherBenefits,
                    kaizenForm.Department ?? "",
                    kaizenForm.Id
                );

                Console.WriteLine($"Found {similarKaizens.Count()} similar kaizen suggestions");

                // Send enhanced email with similar suggestions
                var emailSent = await _emailService.SendKaizenNotificationWithSimilarSuggestionsAsync(
                    engineer.Email ?? "",
                    kaizenForm.KaizenNo,
                    kaizenForm.EmployeeName,
                    kaizenForm.Department ?? "",
                    kaizenForm.SuggestionDescription,
                    websiteUrl,
                    similarKaizens
                );

                if (emailSent)
                {
                    Console.WriteLine($"Email sent successfully to {engineer.Email}");
                }
                else
                {
                    Console.WriteLine($"Failed to send email to {engineer.Email}");
                }

                Console.WriteLine($"=== END EMAIL NOTIFICATION ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email notification: {ex.Message}");
                // Don't throw the exception to avoid breaking the kaizen submission process
            }
        }

        // Send email notification to manager in the department after engineer review
        private async Task SendManagerEmailNotification(KaizenForm kaizenForm, string engineerName)
        {
            try
            {
                Console.WriteLine($"=== SENDING MANAGER EMAIL NOTIFICATION ===");
                Console.WriteLine($"Kaizen No: {kaizenForm.KaizenNo}");
                Console.WriteLine($"Department: {kaizenForm.Department}");
                Console.WriteLine($"Engineer: {engineerName}");

                // Find manager in the same department
                var manager = await _context.Users
                    .Where(u => u.DepartmentName == kaizenForm.Department && 
                               u.Role.ToLower() == "manager" && 
                               !string.IsNullOrEmpty(u.Email))
                    .FirstOrDefaultAsync();

                if (manager == null)
                {
                    Console.WriteLine($"No manager found in department: {kaizenForm.Department}");
                    return;
                }

                Console.WriteLine($"Found manager: {manager.EmployeeName} ({manager.Email})");

                // Generate website URL
                var websiteUrl = $"{Request.Scheme}://{Request.Host}";
                Console.WriteLine($"Website URL: {websiteUrl}");

                // Send email
                var emailSent = await _emailService.SendManagerNotificationAsync(
                    manager.Email ?? "",
                    kaizenForm.KaizenNo,
                    kaizenForm.EmployeeName,
                    kaizenForm.Department ?? "",
                    engineerName ?? "Engineer",
                    kaizenForm.Comments,
                    websiteUrl
                );

                if (emailSent)
                {
                    Console.WriteLine($"Manager email sent successfully to {manager.Email}");
                }
                else
                {
                    Console.WriteLine($"Failed to send manager email to {manager.Email}");
                }

                Console.WriteLine($"=== END MANAGER EMAIL NOTIFICATION ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending manager email notification: {ex.Message}");
                // Don't throw the exception to avoid breaking the review submission process
            }
        }

        // Send email notification to engineers in specified departments for inter-department implementation
        private async Task SendInterDepartmentEmailNotifications(KaizenForm kaizenForm, string implementationArea)
        {
            try
            {
                Console.WriteLine($"=== SENDING INTER-DEPARTMENT EMAIL NOTIFICATIONS ===");
                Console.WriteLine($"Kaizen No: {kaizenForm.KaizenNo}");
                Console.WriteLine($"Source Department: {kaizenForm.Department}");
                Console.WriteLine($"Implementation Area: '{implementationArea}'");

                if (string.IsNullOrEmpty(implementationArea))
                {
                    Console.WriteLine("No implementation area specified, skipping inter-department notifications");
                    return;
                }

                // Parse the implementation area to get individual departments
                var allDepartments = implementationArea.Split(',')
                    .Select(d => d.Trim())
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToList();

                Console.WriteLine($"All departments from implementation area: {string.Join(", ", allDepartments)}");

                var targetDepartments = allDepartments
                    .Where(d => d != kaizenForm.Department)
                    .ToList();

                if (!targetDepartments.Any())
                {
                    Console.WriteLine($"No target departments found (excluding source department: {kaizenForm.Department})");
                    return;
                }

                Console.WriteLine($"Target departments (excluding source): {string.Join(", ", targetDepartments)}");

                // Generate website URL
                var websiteUrl = $"{Request.Scheme}://{Request.Host}";
                Console.WriteLine($"Website URL: {websiteUrl}");

                int totalEmailsSent = 0;
                int totalEmailsFailed = 0;

                // Find engineers in each target department
                foreach (var targetDepartment in targetDepartments)
                {
                    Console.WriteLine($"Processing department: {targetDepartment}");
                    
                    var engineers = await _context.Users
                        .Where(u => u.DepartmentName == targetDepartment && 
                                   u.Role.ToLower() == "engineer" && 
                                   !string.IsNullOrEmpty(u.Email))
                        .ToListAsync();

                    if (!engineers.Any())
                    {
                        Console.WriteLine($"No engineers found in department: {targetDepartment}");
                        continue;
                    }

                    Console.WriteLine($"Found {engineers.Count} engineers in department: {targetDepartment}");

                    // Send email to each engineer
                    foreach (var engineer in engineers)
                    {
                        Console.WriteLine($"Sending email to: {engineer.EmployeeName} ({engineer.Email}) in {targetDepartment}");
                        
                        // Find similar kaizen suggestions for the target department
                        var similarKaizens = await _kaizenService.GetSimilarKaizensAsync(
                            kaizenForm.SuggestionDescription,
                            kaizenForm.CostSavingType,
                            kaizenForm.OtherBenefits,
                            targetDepartment,
                            kaizenForm.Id
                        );

                        Console.WriteLine($"Found {similarKaizens.Count()} similar kaizen suggestions for {targetDepartment}");
                        
                        var emailSent = await _emailService.SendInterDepartmentNotificationWithSimilarSuggestionsAsync(
                            engineer.Email ?? "",
                            kaizenForm.KaizenNo,
                            kaizenForm.EmployeeName,
                            kaizenForm.Department ?? "",
                            targetDepartment,
                            kaizenForm.SuggestionDescription,
                            websiteUrl,
                            similarKaizens
                        );

                        if (emailSent)
                        {
                            Console.WriteLine($"✓ Inter-department email sent successfully to {engineer.EmployeeName} ({engineer.Email}) in {targetDepartment}");
                            totalEmailsSent++;
                        }
                        else
                        {
                            Console.WriteLine($"✗ Failed to send inter-department email to {engineer.EmployeeName} ({engineer.Email}) in {targetDepartment}");
                            totalEmailsFailed++;
                        }
                    }
                }

                Console.WriteLine($"=== EMAIL SUMMARY ===");
                Console.WriteLine($"Total emails sent: {totalEmailsSent}");
                Console.WriteLine($"Total emails failed: {totalEmailsFailed}");
                Console.WriteLine($"Total departments processed: {targetDepartments.Count}");
                Console.WriteLine($"=== END INTER-DEPARTMENT EMAIL NOTIFICATIONS ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending inter-department email notifications: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't throw the exception to avoid breaking the review submission process
            }
        }

        // ------------------- ENGINEER FUNCTIONALITY -------------------

        // GET: /Kaizen/InterDeptSuggestions - For engineers to view kaizens from their department
        [HttpGet]
        public async Task<IActionResult> InterDeptSuggestions(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers (users with "engineer" in their username)
            if (!IsEngineerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== INTERDEPTSUGGESTIONS DEBUG ===");
                Console.WriteLine($"InterDeptSuggestions called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                ViewBag.UserDepartment = userDepartment;
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only - this is the key difference from KaizenListEngineer
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    // Handle multiple implementation areas (comma-separated) and any occurrence of department name
                    query = query.Where(k => 
                        k.ImplementationArea == userDepartment || 
                        k.ImplementationArea.StartsWith(userDepartment + ",") ||
                        k.ImplementationArea.EndsWith("," + userDepartment) ||
                        k.ImplementationArea.Contains("," + userDepartment + ",") ||
                        k.ImplementationArea.Contains(userDepartment)
                    );
                    Console.WriteLine($"Filtered by user department (ImplementationArea): {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/InterDeptSuggestions.cshtml", new List<KaizenForm>());
                }

                // Engineers can see all kaizens in their department, including pending ones
                Console.WriteLine("Engineers can see all kaizens in their department including pending ones");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        query = query.Where(k => k.ManagerStatus == null || k.ManagerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"InterDeptSuggestions returned {kaizens.Count} results (all kaizens in department)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}'");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    Comments: '{k.Comments}'");
                    Console.WriteLine($"    CanImplementInOtherFields: '{k.CanImplementInOtherFields}'");
                }
                
                Console.WriteLine($"=== END INTERDEPTSUGGESTIONS DEBUG ===");

                return View("~/Views/Kaizen/InterDeptSuggestions.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InterDeptSuggestions: {ex.Message}");
                return View("~/Views/Kaizen/InterDeptSuggestions.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/SupervisorInterDeptSuggestions - For supervisors to view approved inter-department suggestions
        [HttpGet]
        public async Task<IActionResult> SupervisorInterDeptSuggestions(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== SUPERVISORINTERDEPTSUGGESTIONS DEBUG ===");
                Console.WriteLine($"SupervisorInterDeptSuggestions called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                ViewBag.UserDepartment = userDepartment;
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only - show kaizens that are approved in the engineer's Inter-Department Suggestions
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    // Handle multiple implementation areas (comma-separated) and any occurrence of department name
                    query = query.Where(k => 
                        k.ImplementationArea == userDepartment || 
                        k.ImplementationArea.StartsWith(userDepartment + ",") ||
                        k.ImplementationArea.EndsWith("," + userDepartment) ||
                        k.ImplementationArea.Contains("," + userDepartment + ",") ||
                        k.ImplementationArea.Contains(userDepartment)
                    );
                    Console.WriteLine($"Filtered by user department (ImplementationArea): {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/SupervisorInterDeptSuggestions.cshtml", new List<KaizenForm>());
                }

                // Only show kaizens that are approved by engineers (for inter-department suggestions)
                query = query.Where(k => k.EngineerStatus == "Approved");
                Console.WriteLine("Filtered to show only engineer-approved kaizens");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter (should only be "Approved" for this view)
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        query = query.Where(k => k.ManagerStatus == null || k.ManagerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"SupervisorInterDeptSuggestions returned {kaizens.Count} results (approved inter-department suggestions)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}'");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    ImplementationArea: '{k.ImplementationArea}'");
                }
                
                Console.WriteLine($"=== END SUPERVISORINTERDEPTSUGGESTIONS DEBUG ===");

                return View("~/Views/Kaizen/SupervisorInterDeptSuggestions.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SupervisorInterDeptSuggestions: {ex.Message}");
                return View("~/Views/Kaizen/SupervisorInterDeptSuggestions.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/ManagerInterDeptSuggestions - For managers to view approved inter-department suggestions
        [HttpGet]
        public async Task<IActionResult> ManagerInterDeptSuggestions(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== MANAGERINTERDEPTSUGGESTIONS DEBUG ===");
                Console.WriteLine($"ManagerInterDeptSuggestions called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                ViewBag.UserDepartment = userDepartment;
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only - show kaizens that are approved in the engineer's Inter-Department Suggestions
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    // Handle multiple implementation areas (comma-separated) and any occurrence of department name
                    query = query.Where(k => 
                        k.ImplementationArea == userDepartment || 
                        k.ImplementationArea.StartsWith(userDepartment + ",") ||
                        k.ImplementationArea.EndsWith("," + userDepartment) ||
                        k.ImplementationArea.Contains("," + userDepartment + ",") ||
                        k.ImplementationArea.Contains(userDepartment)
                    );
                    Console.WriteLine($"Filtered by user department (ImplementationArea): {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/ManagerInterDeptSuggestions.cshtml", new List<KaizenForm>());
                }

                // Only show kaizens that are approved by engineers (for inter-department suggestions)
                query = query.Where(k => k.EngineerStatus == "Approved");
                Console.WriteLine("Filtered to show only engineer-approved kaizens");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter (should only be "Approved" for this view)
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        query = query.Where(k => k.ManagerStatus == null || k.ManagerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"ManagerInterDeptSuggestions returned {kaizens.Count} results (approved inter-department suggestions)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}'");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    ImplementationArea: '{k.ImplementationArea}'");
                }
                
                Console.WriteLine($"=== END MANAGERINTERDEPTSUGGESTIONS DEBUG ===");

                return View("~/Views/Kaizen/ManagerInterDeptSuggestions.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManagerInterDeptSuggestions: {ex.Message}");
                return View("~/Views/Kaizen/ManagerInterDeptSuggestions.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/ManagerInterDeptKaizenDetails - For managers to view inter-department kaizen details
        [HttpGet]
        public async Task<IActionResult> ManagerInterDeptKaizenDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== MANAGERINTERDEPTKAIZENDETAILS DEBUG ===");
                Console.WriteLine($"ManagerInterDeptKaizenDetails called by: {User?.Identity?.Name} for kaizen ID: {id}");

                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    Console.WriteLine($"Kaizen with ID {id} not found");
                    TempData["ErrorMessage"] = "Kaizen not found.";
                    return RedirectToAction("ManagerInterDeptSuggestions");
                }

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Verify that this kaizen is relevant to the manager's department
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    bool isRelevant = kaizen.ImplementationArea == userDepartment || 
                                     kaizen.ImplementationArea.StartsWith(userDepartment + ",") ||
                                     kaizen.ImplementationArea.EndsWith("," + userDepartment) ||
                                     kaizen.ImplementationArea.Contains("," + userDepartment + ",");

                    if (!isRelevant)
                    {
                        Console.WriteLine($"Kaizen {id} is not relevant to manager's department {userDepartment}");
                        TempData["ErrorMessage"] = "You don't have permission to view this kaizen.";
                        return RedirectToAction("ManagerInterDeptSuggestions");
                    }
                }

                // Verify that the kaizen is approved by engineer
                if (kaizen.EngineerStatus != "Approved")
                {
                    Console.WriteLine($"Kaizen {id} is not approved by engineer (Status: {kaizen.EngineerStatus})");
                    TempData["ErrorMessage"] = "This kaizen is not approved by an engineer.";
                    return RedirectToAction("ManagerInterDeptSuggestions");
                }

                Console.WriteLine($"Successfully retrieved kaizen {id} for manager view");
                Console.WriteLine($"=== END MANAGERINTERDEPTKAIZENDETAILS DEBUG ===");

                return View("~/Views/Kaizen/ManagerInterDeptKaizenDetails.cshtml", kaizen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManagerInterDeptKaizenDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while retrieving the kaizen details.";
                return RedirectToAction("ManagerInterDeptSuggestions");
            }
        }

        // GET: /Kaizen/SupervisorInterDeptKaizenDetails - For supervisors to view inter-department kaizen details
        [HttpGet]
        public async Task<IActionResult> SupervisorInterDeptKaizenDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== SUPERVISORINTERDEPTKAIZENDETAILS DEBUG ===");
                Console.WriteLine($"SupervisorInterDeptKaizenDetails called by: {User?.Identity?.Name} for kaizen ID: {id}");

                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    Console.WriteLine($"Kaizen with ID {id} not found");
                    TempData["ErrorMessage"] = "Kaizen not found.";
                    return RedirectToAction("SupervisorInterDeptSuggestions");
                }

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Verify that this kaizen is relevant to the supervisor's department
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    bool isRelevant = kaizen.ImplementationArea == userDepartment || 
                                     kaizen.ImplementationArea.StartsWith(userDepartment + ",") ||
                                     kaizen.ImplementationArea.EndsWith("," + userDepartment) ||
                                     kaizen.ImplementationArea.Contains("," + userDepartment + ",");

                    if (!isRelevant)
                    {
                        Console.WriteLine($"Kaizen {id} is not relevant to supervisor's department {userDepartment}");
                        TempData["ErrorMessage"] = "You don't have permission to view this kaizen.";
                        return RedirectToAction("SupervisorInterDeptSuggestions");
                    }
                }

                // Verify that the kaizen is approved by engineer
                if (kaizen.EngineerStatus != "Approved")
                {
                    Console.WriteLine($"Kaizen {id} is not approved by engineer (Status: {kaizen.EngineerStatus})");
                    TempData["ErrorMessage"] = "This kaizen is not approved by an engineer.";
                    return RedirectToAction("SupervisorInterDeptSuggestions");
                }

                Console.WriteLine($"Successfully retrieved kaizen {id} for supervisor view");
                Console.WriteLine($"=== END SUPERVISORINTERDEPTKAIZENDETAILS DEBUG ===");

                return View("~/Views/Kaizen/SupervisorInterDeptKaizenDetails.cshtml", kaizen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SupervisorInterDeptKaizenDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while retrieving the kaizen details.";
                return RedirectToAction("SupervisorInterDeptSuggestions");
            }
        }

        // GET: /Kaizen/SupervisorKaizenDetails - For supervisors to view regular kaizen details from their department
        [HttpGet]
        public async Task<IActionResult> SupervisorKaizenDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== SUPERVISORKAIZENDETAILS DEBUG ===");
                Console.WriteLine($"SupervisorKaizenDetails called by: {User?.Identity?.Name} for kaizen ID: {id}");

                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    Console.WriteLine($"Kaizen with ID {id} not found");
                    TempData["ErrorMessage"] = "Kaizen not found.";
                    return RedirectToAction("SupervisorKaizenList");
                }

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Verify that this kaizen is from the supervisor's department
                if (!string.IsNullOrEmpty(userDepartment) && kaizen.Department != userDepartment)
                {
                    Console.WriteLine($"Kaizen {id} is not from supervisor's department {userDepartment}");
                    TempData["ErrorMessage"] = "You don't have permission to view this kaizen.";
                    return RedirectToAction("SupervisorKaizenList");
                }

                Console.WriteLine($"Successfully retrieved kaizen {id} for supervisor view");

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                Console.WriteLine($"=== END SUPERVISORKAIZENDETAILS DEBUG ===");

                return View("~/Views/Kaizen/SupervisorKaizenDetails.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SupervisorKaizenDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while retrieving the kaizen details.";
                return RedirectToAction("SupervisorKaizenList");
            }
        }

        // GET: /Kaizen/KaizenListEngineer - For users with "engineer" in their username
        [HttpGet]
        public async Task<IActionResult> KaizenListEngineer(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers (users with "engineer" in their username)
            if (!IsEngineerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== KAIZENLISTENGINEER DEBUG ===");
                Console.WriteLine($"KaizenListEngineer called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by user department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/KaizenListEngineer.cshtml", new List<KaizenForm>());
                }

                // Engineers can see all kaizens in their department, including pending ones
                // No filtering for executive filling - engineers need to see pending items for approval
                Console.WriteLine("Engineers can see all kaizens in their department including pending ones");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        query = query.Where(k => k.ManagerStatus == null || k.ManagerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListEngineer returned {kaizens.Count} results (all kaizens in department)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}'");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    Comments: '{k.Comments}'");
                    Console.WriteLine($"    CanImplementInOtherFields: '{k.CanImplementInOtherFields}'");
                }
                
                Console.WriteLine($"=== END KAIZENLISTENGINEER DEBUG ===");

                return View("~/Views/Kaizen/KaizenListEngineer.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenListEngineer: {ex.Message}");
                return View("~/Views/Kaizen/KaizenListEngineer.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/KaizenListManager - For users with "manager" in their username
        [HttpGet]
        public async Task<IActionResult> KaizenListManager(string searchString, string status, string category, string engineerStatus, string managerStatus, string startDate, string endDate)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "manager" in their username
            var username = User?.Identity?.Name;
            if (username == null || !username.ToLower().Contains("manager"))
            {
                // Redirect based on user role
                if (IsUserRole())
                {
                    return RedirectToAction("Kaizenform");
                }
                else if (IsKaizenTeamRole())
                {
                    return RedirectToAction("KaizenTeam");
                }
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard"); // Default fallback
                }
            }

            try
            {
                Console.WriteLine($"=== KAIZENLISTMANAGER DEBUG ===");
                Console.WriteLine($"KaizenListManager called by: {username}");
                Console.WriteLine($"SearchString: {searchString}, Status: {status}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}, StartDate: {startDate}, EndDate: {endDate}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by user department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/KaizenListManager.cshtml", new List<KaizenForm>());
                }

                // Debug: Check query before filters
                var beforeFilters = await query.CountAsync();
                Console.WriteLine($"Kaizens before filters: {beforeFilters}");
                Console.WriteLine("Note: Cost saving is NOT used as a filter criterion");

                // Debug: Check EngineerStatus values before filtering
                var engineerStatusCounts = await query
                    .GroupBy(k => k.EngineerStatus)
                    .Select(g => new { Status = g.Key ?? "NULL", Count = g.Count() })
                    .ToListAsync();
                Console.WriteLine("EngineerStatus distribution before filtering:");
                foreach (var statusItem in engineerStatusCounts)
                {
                    Console.WriteLine($"  - {statusItem.Status}: {statusItem.Count}");
                }

                // Show all kaizens in the database (no engineer status filter)
                Console.WriteLine("Showing all kaizens in the database");
                
                // Debug: Check count after EngineerStatus filter
                var afterEngineerStatusFilter = await query.CountAsync();
                Console.WriteLine($"Kaizens after EngineerStatus filter: {afterEngineerStatusFilter}");

                // Note: For rejected kaizens, we don't require executive filling data
                Console.WriteLine("No executive filling filter applied for rejected kaizens");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Filter by engineer status
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Filter by manager status
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        // For pending manager status, exclude kaizens that have been rejected by engineer
                        query = query.Where(k => (k.ManagerStatus == null || k.ManagerStatus == "Pending") && 
                                                (k.EngineerStatus != "Rejected"));
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListManager returned {kaizens.Count} results (engineer approved with executive filling)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}'");
                    Console.WriteLine($"    ManagerStatus: '{k.ManagerStatus}'");
                    Console.WriteLine($"    CostSaving: '{k.CostSaving}' (not used in filtering)");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    Comments: '{k.Comments}'");
                    Console.WriteLine($"    CanImplementInOtherFields: '{k.CanImplementInOtherFields}'");

                }
                
                Console.WriteLine($"=== END KAIZENLISTMANAGER DEBUG ===");

                return View("~/Views/Kaizen/KaizenListManager.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenListManager: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return View("~/Views/Kaizen/KaizenListManager.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/KaizenListManagerDebug - Debug version that shows all kaizens
        [HttpGet]
        public async Task<IActionResult> KaizenListManagerDebug(string searchString, string status)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "manager" in their username
            var username = User?.Identity?.Name;
            if (username == null || !username.ToLower().Contains("manager"))
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                Console.WriteLine($"=== KAIZENLISTMANAGER DEBUG VERSION ===");
                Console.WriteLine($"KaizenListManagerDebug called by: {username}");
                Console.WriteLine($"SearchString: {searchString}, Status: {status}");

                var query = _context.KaizenForms.AsQueryable();

                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                // Filter by user's department only
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by user department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Kaizen/KaizenListManager.cshtml", new List<KaizenForm>());
                }

                // Debug: Check query before any filters
                var beforeFilters = await query.CountAsync();
                Console.WriteLine($"Kaizens before any filters: {beforeFilters}");

                // Show all kaizens (no executive filling filter for debugging)
                Console.WriteLine("DEBUG MODE: Showing all kaizens without executive filling filter");

                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(k => k.ManagerStatus == status);
                    Console.WriteLine($"Applied manager status filter: {status}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListManagerDebug returned {kaizens.Count} results");
                
                // Debug: Show sample results with executive filling status
                foreach (var k in kaizens.Take(5))
                {
                    var hasExecutiveFilling = !string.IsNullOrEmpty(k.Category) &&
                                            !string.IsNullOrEmpty(k.Comments) &&
                                            !string.IsNullOrEmpty(k.CanImplementInOtherFields);
                    
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department}) - Has Executive Filling: {hasExecutiveFilling}");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    Comments: '{k.Comments}'");
                    Console.WriteLine($"    CanImplementInOtherFields: '{k.CanImplementInOtherFields}'");
                }
                
                Console.WriteLine($"=== END KAIZENLISTMANAGER DEBUG VERSION ===");

                return View("~/Views/Kaizen/KaizenListManager.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenListManagerDebug: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return View("~/Views/Kaizen/KaizenListManager.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/UserKaizenList - For users with "user" in their username
        [HttpGet]
        public async Task<IActionResult> UserKaizenList(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "user" in their username
            if (!IsUserRole())
            {
                // Check if user is a manager
                if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                // Check if user is an engineer
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard");
                }
            }

            try
            {
                var username = User?.Identity?.Name;
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"=== USERKAIZENLIST DEBUG ===");
                Console.WriteLine($"UserKaizenList called by: {username}, UserDepartment: {userDepartment}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();
                
                // Debug: Check initial query count
                var initialCount = await query.CountAsync();
                Console.WriteLine($"UserKaizenList initial query count: {initialCount}");

                // Always filter by user's department for users
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by user department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No user department found, showing no results");
                    return View("~/Views/Home/UserKaizenList.cshtml", new List<KaizenForm>());
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        // For pending manager status, exclude kaizens that have been rejected by engineer
                        query = query.Where(k => (k.ManagerStatus == null || k.ManagerStatus == "Pending") && 
                                                (k.EngineerStatus != "Rejected"));
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                // Apply search filter if search string is provided
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Debug: Check query before final execution
                var queryCount = await query.CountAsync();
                Console.WriteLine($"UserKaizenList query count before final execution: {queryCount}");
                
                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"UserKaizenList returned {kaizens.Count} results");
                Console.WriteLine($"=== END USERKAIZENLIST DEBUG ===");

                return View("~/Views/Home/UserKaizenList.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserKaizenList: {ex.Message}");
                return View("~/Views/Home/UserKaizenList.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/SupervisorKaizenList - For supervisors to view all kaizens from their department
        [HttpGet]
        public async Task<IActionResult> SupervisorKaizenList(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                TempData["AlertMessage"] = "Access Denied: Only supervisors can access this page.";
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var username = User?.Identity?.Name;
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"=== SUPERVISORKAIZENLIST DEBUG ===");
                Console.WriteLine($"SupervisorKaizenList called by: {username}, UserDepartment: {userDepartment}");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();
                
                // Debug: Check initial query count
                var initialCount = await query.CountAsync();
                Console.WriteLine($"SupervisorKaizenList initial query count: {initialCount}");

                // Always filter by user's department for supervisors
                if (!string.IsNullOrEmpty(userDepartment))
                {
                    query = query.Where(k => k.Department == userDepartment);
                    Console.WriteLine($"Filtered by supervisor department: {userDepartment}");
                }
                else
                {
                    Console.WriteLine("No supervisor department found, showing no results");
                    return View("~/Views/Kaizen/SupervisorKaizenList.cshtml", new List<KaizenForm>());
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        // For pending manager status, exclude kaizens that have been rejected by engineer
                        query = query.Where(k => (k.ManagerStatus == null || k.ManagerStatus == "Pending") && 
                                                (k.EngineerStatus != "Rejected"));
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                // Apply search filter if search string is provided
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Debug: Check query before final execution
                var queryCount = await query.CountAsync();
                Console.WriteLine($"SupervisorKaizenList query count before final execution: {queryCount}");
                
                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"SupervisorKaizenList returned {kaizens.Count} results");
                Console.WriteLine($"=== END SUPERVISORKAIZENLIST DEBUG ===");

                return View("~/Views/Kaizen/SupervisorKaizenList.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SupervisorKaizenList: {ex.Message}");
                return View("~/Views/Kaizen/SupervisorKaizenList.cshtml", new List<KaizenForm>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyKaizens(string searchString, string startDate, string endDate, string category, string engineerStatus, string managerStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "user" in their username
            if (!IsUserRole())
            {
                // Check if user is a manager
                if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                // Check if user is an engineer
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard");
                }
            }

            try
            {
                var username = User?.Identity?.Name;
                var currentUser = await GetCurrentUserAsync();
                
                Console.WriteLine($"=== MYKAIZENS DEBUG ===");
                Console.WriteLine($"MyKaizens called by: {username}");
                Console.WriteLine($"Current user: {currentUser?.EmployeeName} (EmployeeNumber: '{currentUser?.EmployeeNumber}', Department: {currentUser?.DepartmentName})");
                Console.WriteLine($"Current user username: '{currentUser?.UserName}'");
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}, ManagerStatus: {managerStatus}");

                var query = _context.KaizenForms.AsQueryable();
                
                // Debug: Check initial query count
                var initialCount = await query.CountAsync();
                Console.WriteLine($"MyKaizens initial query count: {initialCount}");

                // Debug: Show some sample kaizen records to understand the data
                var sampleKaizens = await _context.KaizenForms.Take(5).ToListAsync();
                Console.WriteLine($"Sample kaizen records:");
                foreach (var k in sampleKaizens)
                {
                    Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                }
                
                // Debug: Show all unique employee numbers in the kaizen table
                var uniqueEmployeeNumbers = await _context.KaizenForms.Select(k => k.EmployeeNo).Distinct().ToListAsync();
                Console.WriteLine($"All unique employee numbers in kaizen table: {string.Join(", ", uniqueEmployeeNumbers.Select(e => $"'{e}'"))}");
                
                // Debug: Show all users in the database
                var allUsers = await _context.Users.ToListAsync();
                Console.WriteLine($"All users in database:");
                foreach (var u in allUsers)
                {
                    Console.WriteLine($"  - Username: '{u.UserName}', EmployeeName: {u.EmployeeName}, EmployeeNumber: '{u.EmployeeNumber}', Department: {u.DepartmentName}");
                }

                // Filter by current user's employee number
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.EmployeeNumber))
                {
                    var userEmployeeNumber = currentUser.EmployeeNumber.Trim();
                    query = query.Where(k => k.EmployeeNo == userEmployeeNumber);
                    Console.WriteLine($"Filtered by user employee number: '{userEmployeeNumber}'");
                    
                    // Debug: Check if there are any kaizens for this employee number
                    var userKaizens = await _context.KaizenForms.Where(k => k.EmployeeNo == userEmployeeNumber).ToListAsync();
                    Console.WriteLine($"Found {userKaizens.Count} kaizen records for employee number '{userEmployeeNumber}'");
                    foreach (var k in userKaizens)
                    {
                        Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                    }
                    
                    // If no exact matches found, try a more flexible search
                    if (userKaizens.Count == 0)
                    {
                        Console.WriteLine($"No exact matches found for employee number '{userEmployeeNumber}', trying flexible search...");
                        var flexibleMatches = await _context.KaizenForms.Where(k => k.EmployeeNo.Contains(userEmployeeNumber) || userEmployeeNumber.Contains(k.EmployeeNo)).ToListAsync();
                        Console.WriteLine($"Found {flexibleMatches.Count} flexible matches:");
                        foreach (var k in flexibleMatches)
                        {
                            Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                        }
                        
                        // If still no matches, try matching by employee name
                        if (flexibleMatches.Count == 0 && !string.IsNullOrEmpty(currentUser.EmployeeName))
                        {
                            Console.WriteLine($"No flexible matches found, trying to match by employee name: '{currentUser.EmployeeName}'");
                            var nameMatches = await _context.KaizenForms.Where(k => k.EmployeeName == currentUser.EmployeeName).ToListAsync();
                            Console.WriteLine($"Found {nameMatches.Count} name matches:");
                            foreach (var k in nameMatches)
                            {
                                Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                            }
                            
                            // If name matches found, use them instead
                            if (nameMatches.Count > 0)
                            {
                                Console.WriteLine($"Using name matches instead of employee number matches");
                                query = _context.KaizenForms.Where(k => k.EmployeeName == currentUser.EmployeeName);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No user employee number found, showing no results");
                    return View("~/Views/Home/MyKaizens.cshtml", new List<KaizenForm>());
                }
                
                // Debug: If no results found, show all kaizens for debugging
                var finalCount = await query.CountAsync();
                if (finalCount == 0)
                {
                    Console.WriteLine("No kaizens found for current user, showing all kaizens for debugging:");
                    var allKaizens = await _context.KaizenForms.Take(10).ToListAsync();
                    foreach (var k in allKaizens)
                    {
                        Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                    }
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
                }

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                    Console.WriteLine($"Applied category filter: {category}");
                }

                // Apply engineer status filter
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    if (engineerStatus == "Pending")
                    {
                        query = query.Where(k => k.EngineerStatus == null || k.EngineerStatus == "Pending");
                    }
                    else
                    {
                        query = query.Where(k => k.EngineerStatus == engineerStatus);
                    }
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
                }

                // Apply manager status filter
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    if (managerStatus == "Pending")
                    {
                        // For pending manager status, exclude kaizens that have been rejected by engineer
                        query = query.Where(k => (k.ManagerStatus == null || k.ManagerStatus == "Pending") && 
                                                (k.EngineerStatus != "Rejected"));
                    }
                    else
                    {
                        query = query.Where(k => k.ManagerStatus == managerStatus);
                    }
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }

                // Apply search filter if search string is provided
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Debug: Check query before final execution
                var queryCount = await query.CountAsync();
                Console.WriteLine($"MyKaizens query count before final execution: {queryCount}");
                
                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"MyKaizens returned {kaizens.Count} results");
                
                // Debug: Show the final results
                foreach (var k in kaizens)
                {
                    Console.WriteLine($"  - KaizenNo: {k.KaizenNo}, EmployeeNo: '{k.EmployeeNo}', EmployeeName: {k.EmployeeName}");
                }
                
                Console.WriteLine($"=== END MYKAIZENS DEBUG ===");

                return View("~/Views/Home/MyKaizens.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MyKaizens: {ex.Message}");
                return View("~/Views/Home/MyKaizens.cshtml", new List<KaizenForm>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Leaderboard(string department, string quarter, string searchString)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                Console.WriteLine($"=== LEADERBOARD DEBUG ===");
                Console.WriteLine($"Leaderboard called by: {User?.Identity?.Name}");
                Console.WriteLine($"Department: {department}, Quarter: {quarter}, SearchString: {searchString}");



                var query = _context.KaizenForms.AsQueryable();

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                    Console.WriteLine($"Filtered by department: {department}");
                }

                // Apply quarter filter (current year only)
                if (!string.IsNullOrEmpty(quarter))
                {
                    var quarterNumber = int.Parse(quarter);
                    var quarterStart = new DateTime(DateTime.Now.Year, (quarterNumber - 1) * 3 + 1, 1);
                    var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                    query = query.Where(k => k.DateSubmitted >= quarterStart && k.DateSubmitted <= quarterEnd);
                    Console.WriteLine($"Filtered by quarter: Q{quarter} {DateTime.Now.Year}");
                }

                // Apply search filter if search string is provided
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower)
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Get leaderboard data grouped by employee number only
                var leaderboardData = await query
                    .Where(k => !string.IsNullOrEmpty(k.EmployeeNo))
                    .GroupBy(k => k.EmployeeNo)
                    .Select(g => new
                    {
                        EmployeeNo = g.Key,
                        EmployeeName = g.First().EmployeeName, // Take the first occurrence of the name
                        EmployeePhotoPath = g.First().EmployeePhotoPath, // Take the first occurrence of the photo
                        Department = g.First().Department, // Take the first occurrence of the department
                        TotalKaizens = g.Count(),
                        ApprovedKaizens = g.Count(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved"),
                        PendingKaizens = g.Count(k => (k.EngineerStatus == null || k.EngineerStatus == "Pending") || 
                                                     (k.EngineerStatus == "Approved" && (k.ManagerStatus == null || k.ManagerStatus == "Pending"))),
                        RejectedKaizens = g.Count(k => k.EngineerStatus == "Rejected" || k.ManagerStatus == "Rejected"),
                        TotalCostSaving = g.Where(k => k.CostSaving.HasValue).Sum(k => k.CostSaving.Value),
                        LastSubmission = g.Max(k => k.DateSubmitted)
                    })
                    .OrderByDescending(x => x.TotalKaizens)
                    .ThenByDescending(x => x.ApprovedKaizens)
                    .ThenByDescending(x => x.TotalCostSaving)
                    .ToListAsync();

                // If searching for a specific employee, get their actual rank from the full leaderboard
                List<LeaderboardViewModel> rankedLeaderboard;
                
                if (!string.IsNullOrEmpty(searchString))
                {
                    // Get the full leaderboard without search filter to determine actual ranks
                    var fullLeaderboardQuery = _context.KaizenForms.AsQueryable();
                    
                    // Apply department and quarter filters to full leaderboard
                    if (!string.IsNullOrEmpty(department))
                    {
                        fullLeaderboardQuery = fullLeaderboardQuery.Where(k => k.Department == department);
                    }
                    
                    if (!string.IsNullOrEmpty(quarter))
                    {
                        var quarterNumber = int.Parse(quarter);
                        var quarterStart = new DateTime(DateTime.Now.Year, (quarterNumber - 1) * 3 + 1, 1);
                        var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                        fullLeaderboardQuery = fullLeaderboardQuery.Where(k => k.DateSubmitted >= quarterStart && k.DateSubmitted <= quarterEnd);
                    }

                    var fullLeaderboardData = await fullLeaderboardQuery
                        .Where(k => !string.IsNullOrEmpty(k.EmployeeNo))
                        .GroupBy(k => k.EmployeeNo)
                        .Select(g => new
                        {
                            EmployeeNo = g.Key,
                            EmployeeName = g.First().EmployeeName, // Take the first occurrence of the name
                            EmployeePhotoPath = g.First().EmployeePhotoPath, // Take the first occurrence of the photo
                            Department = g.First().Department, // Take the first occurrence of the department
                            TotalKaizens = g.Count(),
                            ApprovedKaizens = g.Count(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved"),
                            PendingKaizens = g.Count(k => (k.EngineerStatus == null || k.EngineerStatus == "Pending") || 
                                                         (k.EngineerStatus == "Approved" && (k.ManagerStatus == null || k.ManagerStatus == "Pending"))),
                            RejectedKaizens = g.Count(k => k.EngineerStatus == "Rejected" || k.ManagerStatus == "Rejected"),
                            TotalCostSaving = g.Where(k => k.CostSaving.HasValue).Sum(k => k.CostSaving.Value),
                            LastSubmission = g.Max(k => k.DateSubmitted)
                        })
                        .OrderByDescending(x => x.TotalKaizens)
                        .ThenByDescending(x => x.ApprovedKaizens)
                        .ThenByDescending(x => x.TotalCostSaving)
                        .ToListAsync();

                    // Create a dictionary to map employee numbers to their actual ranks
                    var rankDictionary = fullLeaderboardData.Select((entry, index) => new { entry.EmployeeNo, Rank = index + 1 })
                        .ToDictionary(x => x.EmployeeNo, x => x.Rank);

                    // Add actual ranks to the filtered results
                    rankedLeaderboard = leaderboardData.Select(entry => new LeaderboardViewModel
                    {
                        Rank = rankDictionary.ContainsKey(entry.EmployeeNo) ? rankDictionary[entry.EmployeeNo] : 999,
                        EmployeeNo = entry.EmployeeNo,
                        EmployeeName = entry.EmployeeName,
                        EmployeePhotoPath = entry.EmployeePhotoPath,
                        Department = entry.Department,
                        TotalKaizens = entry.TotalKaizens,
                        ApprovedKaizens = entry.ApprovedKaizens,
                        PendingKaizens = entry.PendingKaizens,
                        RejectedKaizens = entry.RejectedKaizens,
                        TotalCostSaving = entry.TotalCostSaving,
                        LastSubmission = entry.LastSubmission
                    }).OrderBy(x => x.Rank).ToList();
                }
                else
                {
                    // Add rank to each entry (normal case without search)
                    rankedLeaderboard = leaderboardData.Select((entry, index) => new LeaderboardViewModel
                    {
                        Rank = index + 1,
                        EmployeeNo = entry.EmployeeNo,
                        EmployeeName = entry.EmployeeName,
                        EmployeePhotoPath = entry.EmployeePhotoPath,
                        Department = entry.Department,
                        TotalKaizens = entry.TotalKaizens,
                        ApprovedKaizens = entry.ApprovedKaizens,
                        PendingKaizens = entry.PendingKaizens,
                        RejectedKaizens = entry.RejectedKaizens,
                        TotalCostSaving = entry.TotalCostSaving,
                        LastSubmission = entry.LastSubmission
                    }).ToList();
                }

                Console.WriteLine($"Leaderboard returned {rankedLeaderboard.Count} entries");
                
                // Debug: Show the grouped data
                Console.WriteLine("Grouped leaderboard data:");
                foreach (var entry in rankedLeaderboard)
                {
                    Console.WriteLine($"  - {entry.EmployeeName} ({entry.EmployeeNo}): {entry.TotalKaizens} total kaizens");
                }
                

                
                Console.WriteLine($"=== END LEADERBOARD DEBUG ===");

                // Get unique departments for filter dropdown
                var departments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                ViewBag.Departments = departments;
                ViewBag.SelectedDepartment = department;
                ViewBag.SelectedQuarter = quarter;
                ViewBag.SearchString = searchString;

                return View("~/Views/Home/Leaderboard.cshtml", rankedLeaderboard);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Leaderboard: {ex.Message}");
                return View("~/Views/Home/Leaderboard.cshtml", new List<LeaderboardViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers and engineers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers and engineers can update status." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Handle the name parameter (can be ApproverName or RejectorName)
                string userName = null;
                if (!string.IsNullOrEmpty(request.ApproverName))
                {
                    userName = request.ApproverName;
                }
                else if (!string.IsNullOrEmpty(request.RejectorName))
                {
                    userName = request.RejectorName;
                }

                // Determine if this is an engineer or manager approval
                bool isEngineer = IsEngineerRole();
                bool isManager = IsManagerRole();

                if (isEngineer)
                {
                    // Engineer approval
                    kaizen.EngineerStatus = request.Status;
                    kaizen.EngineerApprovedBy = userName;
                }
                else if (isManager)
                {
                    // Manager approval
                    kaizen.ManagerStatus = request.Status;
                    kaizen.ManagerApprovedBy = userName;
                }
                
                await _context.SaveChangesAsync();

                string approverType = isEngineer ? "Engineer" : "Manager";
                return Json(new { success = true, message = $"{approverType} status updated to {request.Status} successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEngineerStatus(int id, [FromBody] UpdateEngineerStatusRequest request)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers
            if (!IsEngineerRole())
            {
                return Json(new { success = false, message = "Access denied. Only engineers can update engineer status." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Update engineer status
                kaizen.EngineerStatus = request.EngineerStatus;
                kaizen.EngineerApprovedBy = request.EngineerApprovedBy;
                
                await _context.SaveChangesAsync();

                // Only send manager email notification if engineer approved the kaizen
                if (request.EngineerStatus == "Approved")
                {
                    Console.WriteLine($"=== ENGINEER APPROVED - SENDING MANAGER EMAIL ===");
                    Console.WriteLine($"Kaizen No: {kaizen.KaizenNo}");
                    Console.WriteLine($"Engineer: {request.EngineerApprovedBy}");
                    
                    await SendManagerEmailNotification(kaizen, request.EngineerApprovedBy);
                    
                    Console.WriteLine($"=== END ENGINEER APPROVED - MANAGER EMAIL ===");
                }
                else
                {
                    Console.WriteLine($"=== ENGINEER REJECTED - NO MANAGER EMAIL ===");
                    Console.WriteLine($"Kaizen No: {kaizen.KaizenNo}");
                    Console.WriteLine($"Engineer: {request.EngineerApprovedBy}");
                    Console.WriteLine($"Status: {request.EngineerStatus}");
                    Console.WriteLine($"Skipping manager email notification");
                    Console.WriteLine($"=== END ENGINEER REJECTED - NO MANAGER EMAIL ===");
                }

                return Json(new { success = true, message = $"Engineer status updated to {request.EngineerStatus} successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateManagerStatus(int id, [FromBody] UpdateManagerStatusRequest request)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can update manager status." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Update manager status
                kaizen.ManagerStatus = request.ManagerStatus;
                kaizen.ManagerApprovedBy = request.ManagerApprovedBy;
                
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Manager status updated to {request.ManagerStatus} successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> FormB(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return NotFound();
                }

                var formBViewModel = new FormBViewModel
                {
                    Id = kaizen.Id,
                    KaizenNo = kaizen.KaizenNo,
                    EmployeeName = kaizen.EmployeeName,
                    EmployeeNo = kaizen.EmployeeNo,
                    Department = kaizen.Department,
                    SuggestionDescription = kaizen.SuggestionDescription,
                    BeforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    AfterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    EmployeePhotoPath = kaizen.EmployeePhotoPath,
                    OtherBenefits = kaizen.OtherBenefits,
                    ImplementationDate = kaizen.DateImplemented ?? DateTime.Now,
                    ImplementationCost = 0,
                    ImplementationDetails = "",
                    Results = "",
                    Remarks = "",
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature,
                    DateSubmitted = kaizen.DateSubmitted
                };

                return View(formBViewModel);
            }
            catch (Exception)
            {
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFormB(FormBViewModel model)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can save Form B." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(model.Id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Update the kaizen with Form B data
                kaizen.DateImplemented = model.ImplementationDate;
                
                // You could add Form B specific fields to the KaizenForm model if needed
                // For now, we'll store the implementation details in OtherBenefits
                kaizen.OtherBenefits = $"Implementation Cost: ${model.ImplementationCost}\n" +
                                      $"Implementation Details: {model.ImplementationDetails}\n" +
                                      $"Results: {model.Results}\n" +
                                      $"Remarks: {model.Remarks}";

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Form B saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintFormB(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return NotFound();
                }

                return View("PrintFormB", new FormBViewModel
                {
                    Id = kaizen.Id,
                    KaizenNo = kaizen.KaizenNo,
                    EmployeeName = kaizen.EmployeeName,
                    EmployeeNo = kaizen.EmployeeNo,
                    Department = kaizen.Department,
                    SuggestionDescription = kaizen.SuggestionDescription,
                    BeforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    AfterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    EmployeePhotoPath = kaizen.EmployeePhotoPath,
                    OtherBenefits = kaizen.OtherBenefits,
                    ImplementationDate = kaizen.DateImplemented ?? DateTime.Now,
                    ImplementationCost = 0,
                    ImplementationDetails = "",
                    Results = "",
                    Remarks = "",
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature,
                    DateSubmitted = kaizen.DateSubmitted
                });
            }
            catch (Exception)
            {
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetKaizenData(int id)
        {
            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen not found" });
                }

                var kaizenData = new
                {
                    kaizenNo = kaizen.KaizenNo,
                    employeeName = kaizen.EmployeeName,
                    employeeNo = kaizen.EmployeeNo,
                    department = kaizen.Department,
                    suggestionDescription = kaizen.SuggestionDescription,
                    otherBenefits = kaizen.OtherBenefits,
                    beforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    afterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    managerComments = kaizen.ManagerComments,
                    managerSignature = kaizen.ManagerSignature,
                    costSaving = kaizen.CostSaving,
                    category = kaizen.Category,
                    dateSubmitted = kaizen.DateSubmitted.ToString("yyyy-MM-dd")
                };

                return Json(new { success = true, kaizen = kaizenData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/GetCurrentEmployeeData - AJAX endpoint to get current employee data
        [HttpGet]
        public async Task<IActionResult> GetCurrentEmployeeData()
        {
            try
            {
                Console.WriteLine("=== GET CURRENT EMPLOYEE DATA ===");
                
                // Get user information by employee number extracted from username
                var userByEmployeeNumber = await GetUserByEmployeeNumberFromUsernameAsync();
                
                if (userByEmployeeNumber != null)
                {
                    Console.WriteLine($"Found employee: {userByEmployeeNumber.EmployeeName} ({userByEmployeeNumber.EmployeeNumber})");
                    
                    return Json(new { 
                        success = true,
                        employeeName = userByEmployeeNumber.EmployeeName,
                        employeeNo = userByEmployeeNumber.EmployeeNumber,
                        department = userByEmployeeNumber.DepartmentName,
                        plant = userByEmployeeNumber.Plant,
                        employeePhotoPath = userByEmployeeNumber.EmployeePhotoPath
                    });
                }
                else
                {
                    Console.WriteLine("No employee found by employee number - trying fallback method");
                    
                    // Fallback to current user method
                    var currentUser = await GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        Console.WriteLine($"Found employee via fallback: {currentUser.EmployeeName} ({currentUser.EmployeeNumber})");
                        
                        return Json(new { 
                            success = true,
                            employeeName = currentUser.EmployeeName,
                            employeeNo = currentUser.EmployeeNumber,
                            department = currentUser.DepartmentName,
                            plant = currentUser.Plant,
                            employeePhotoPath = currentUser.EmployeePhotoPath
                        });
                    }
                    else
                    {
                        Console.WriteLine("No employee found via any method");
                        return Json(new { 
                            success = false, 
                            message = "No employee data found for current user" 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting employee data: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = "Error retrieving employee data: " + ex.Message 
                });
            }
        }

        // GET: /Kaizen/TestImages - Debug endpoint to test image accessibility
        [HttpGet]
        public async Task<IActionResult> TestImages()
        {
            try
            {
                Console.WriteLine("=== TEST IMAGES ===");
                
                // Get a sample kaizen with images
                var kaizenWithImages = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.EmployeePhotoPath) || 
                                !string.IsNullOrEmpty(k.BeforeKaizenImagePath) || 
                                !string.IsNullOrEmpty(k.AfterKaizenImagePath))
                    .FirstOrDefaultAsync();
                
                if (kaizenWithImages != null)
                {
                    Console.WriteLine($"Found kaizen with images: {kaizenWithImages.KaizenNo}");
                    Console.WriteLine($"EmployeePhotoPath: {kaizenWithImages.EmployeePhotoPath}");
                    Console.WriteLine($"BeforeKaizenImagePath: {kaizenWithImages.BeforeKaizenImagePath}");
                    Console.WriteLine($"AfterKaizenImagePath: {kaizenWithImages.AfterKaizenImagePath}");
                    
                    // Check if files exist
                    var webRootPath = _env.WebRootPath;
                    Console.WriteLine($"WebRootPath: {webRootPath}");
                    
                    if (!string.IsNullOrEmpty(kaizenWithImages.EmployeePhotoPath))
                    {
                        var employeePhotoFullPath = Path.Combine(webRootPath, kaizenWithImages.EmployeePhotoPath.TrimStart('/'));
                        Console.WriteLine($"Employee photo full path: {employeePhotoFullPath}");
                        Console.WriteLine($"Employee photo exists: {System.IO.File.Exists(employeePhotoFullPath)}");
                    }
                    
                    if (!string.IsNullOrEmpty(kaizenWithImages.BeforeKaizenImagePath))
                    {
                        var beforeImageFullPath = Path.Combine(webRootPath, kaizenWithImages.BeforeKaizenImagePath.TrimStart('/'));
                        Console.WriteLine($"Before image full path: {beforeImageFullPath}");
                        Console.WriteLine($"Before image exists: {System.IO.File.Exists(beforeImageFullPath)}");
                    }
                    
                    if (!string.IsNullOrEmpty(kaizenWithImages.AfterKaizenImagePath))
                    {
                        var afterImageFullPath = Path.Combine(webRootPath, kaizenWithImages.AfterKaizenImagePath.TrimStart('/'));
                        Console.WriteLine($"After image full path: {afterImageFullPath}");
                        Console.WriteLine($"After image exists: {System.IO.File.Exists(afterImageFullPath)}");
                    }
                }
                else
                {
                    Console.WriteLine("No kaizen with images found");
                }
                
                Console.WriteLine("=== END TEST IMAGES ===");
                
                return Json(new { 
                    success = true, 
                    message = "Image test completed. Check console for details.",
                    kaizenWithImages = kaizenWithImages != null ? new {
                        kaizenWithImages.KaizenNo,
                        kaizenWithImages.EmployeePhotoPath,
                        kaizenWithImages.BeforeKaizenImagePath,
                        kaizenWithImages.AfterKaizenImagePath
                    } : null
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test images error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Kaizen/TestKaizenListManager - Debug endpoint for KaizenListManager issues
        [HttpGet]
        public async Task<IActionResult> TestDatabaseState()
        {
            try
            {
                var totalKaizens = await _context.KaizenForms.CountAsync();
                var engineerApprovedCount = await _context.KaizenForms.CountAsync(k => k.EngineerStatus == "Approved");
                var managerApprovedCount = await _context.KaizenForms.CountAsync(k => k.ManagerStatus == "Approved");
                var nullEngineerStatusCount = await _context.KaizenForms.CountAsync(k => k.EngineerStatus == null);
                var nullManagerStatusCount = await _context.KaizenForms.CountAsync(k => k.ManagerStatus == null);

                var sampleKaizens = await _context.KaizenForms
                    .Take(5)
                    .Select(k => new
                    {
                        k.Id,
                        k.KaizenNo,
                        k.EmployeeName,
                        k.EngineerStatus,
                        k.ManagerStatus,
                        k.EngineerApprovedBy,
                        k.ManagerApprovedBy
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    totalKaizens,
                    engineerApprovedCount,
                    managerApprovedCount,
                    nullEngineerStatusCount,
                    nullManagerStatusCount,
                    sampleKaizens
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateExistingRecords()
        {
            try
            {
                // Update records where EngineerStatus is null to "Pending"
                var nullEngineerStatusRecords = await _context.KaizenForms
                    .Where(k => k.EngineerStatus == null)
                    .ToListAsync();

                foreach (var record in nullEngineerStatusRecords)
                {
                    record.EngineerStatus = "Pending";
                }

                // Update records where ManagerStatus is null to "Pending"
                var nullManagerStatusRecords = await _context.KaizenForms
                    .Where(k => k.ManagerStatus == null)
                    .ToListAsync();

                foreach (var record in nullManagerStatusRecords)
                {
                    record.ManagerStatus = "Pending";
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Updated {nullEngineerStatusRecords.Count} EngineerStatus records and {nullManagerStatusRecords.Count} ManagerStatus records to 'Pending'"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> TestKaizenListManager()
        {
            try
            {
                Console.WriteLine("=== TEST KAIZENLISTMANAGER DEBUG ===");
                
                var username = User?.Identity?.Name;
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"Current user: {username}");
                Console.WriteLine($"User department: {userDepartment}");
                
                // Get all kaizens in database
                var allKaizens = await _context.KaizenForms.ToListAsync();
                Console.WriteLine($"Total kaizens in database: {allKaizens.Count}");
                
                // Check kaizens by department
                var kaizensByDepartment = allKaizens.GroupBy(k => k.Department).ToList();
                foreach (var group in kaizensByDepartment)
                {
                    Console.WriteLine($"Department '{group.Key}': {group.Count()} kaizens");
                }
                
                // Check kaizens with executive filling
                var kaizensWithExecutiveFilling = allKaizens.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                ).ToList();
                
                Console.WriteLine($"Kaizens with completed executive filling: {kaizensWithExecutiveFilling.Count}");
                
                // Check kaizens by user's department with executive filling
                var userDepartmentKaizens = allKaizens.Where(k => k.Department == userDepartment).ToList();
                Console.WriteLine($"Kaizens in user's department ({userDepartment}): {userDepartmentKaizens.Count}");
                
                var userDepartmentWithExecutiveFilling = userDepartmentKaizens.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                ).ToList();
                
                Console.WriteLine($"Kaizens in user's department with completed executive filling: {userDepartmentWithExecutiveFilling.Count}");
                
                // Show sample data
                Console.WriteLine("Sample kaizens with executive filling:");
                foreach (var k in kaizensWithExecutiveFilling.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    Comments: '{k.Comments}'");
                    Console.WriteLine($"    CanImplementInOtherFields: '{k.CanImplementInOtherFields}'");
                }
                
                Console.WriteLine("=== END TEST KAIZENLISTMANAGER DEBUG ===");
                
                return Json(new { 
                    success = true, 
                    username = username,
                    userDepartment = userDepartment,
                    totalKaizens = allKaizens.Count,
                    kaizensWithExecutiveFilling = kaizensWithExecutiveFilling.Count,
                    userDepartmentKaizens = userDepartmentKaizens.Count,
                    userDepartmentWithExecutiveFilling = userDepartmentWithExecutiveFilling.Count,
                    sampleKaizens = kaizensWithExecutiveFilling.Take(3).Select(k => new { 
                        k.KaizenNo, 
                        k.EmployeeName, 
                        k.Department,
                        k.Category,
                        k.Comments,
                        k.CanImplementInOtherFields
                    })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test KaizenListManager error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }



        // POST: /Kaizen/Update - For updating kaizen data from the edit mode
        [HttpPost]
        public async Task<IActionResult> Update()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers (users with "engineer" in their username)
            if (!IsEngineerRole())
            {
                return Json(new { success = false, message = "Access denied. Only engineers can update kaizens." });
            }

            try
            {
                // Get form data
                var id = Request.Form["id"].ToString();
                if (!int.TryParse(id, out int kaizenId))
                {
                    return Json(new { success = false, message = "Invalid kaizen ID." });
                }

                var kaizen = await _context.KaizenForms.FindAsync(kaizenId);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Update editable fields
                var suggestionDescription = Request.Form["suggestionDescription"].ToString();
                var costSaving = Request.Form["costSaving"].ToString();
                var dollarRate = Request.Form["dollarRate"].ToString();
                var otherBenefits = Request.Form["otherBenefits"].ToString();

                // Update the kaizen record
                if (!string.IsNullOrEmpty(suggestionDescription))
                {
                    kaizen.SuggestionDescription = suggestionDescription.Trim();
                }

                if (!string.IsNullOrEmpty(costSaving) && decimal.TryParse(costSaving, out decimal costSavingValue))
                {
                    kaizen.CostSaving = costSavingValue;
                }

                if (!string.IsNullOrEmpty(dollarRate) && decimal.TryParse(dollarRate, out decimal dollarRateValue))
                {
                    kaizen.DollarRate = dollarRateValue;
                }

                if (!string.IsNullOrEmpty(otherBenefits))
                {
                    kaizen.OtherBenefits = otherBenefits.Trim();
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Kaizen updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // POST: /Kaizen/SaveManagerComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveManagerComment(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users with "manager" in their username)
            if (!IsManagerRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can add comments." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Get form data
                var comments = Request.Form["Comments"].ToString();
                var signature = Request.Form["Signature"].ToString();

                // Update the kaizen with manager comment data
                kaizen.ManagerComments = comments?.Trim();
                kaizen.ManagerSignature = signature?.Trim();

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Manager comment saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // POST: /Kaizen/SaveExecutiveFilling
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExecutiveFilling(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers (users with "engineer" in their username)
            if (!IsEngineerRole())
            {
                return Json(new { success = false, message = "Access denied. Only engineers can fill executive review." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Get form data
                var categories = Request.Form["Category"].ToArray();
                var approvedBy = Request.Form["ApprovedBy"].ToString();
                var comments = Request.Form["Comments"].ToString();
                var canImplementInOtherFields = Request.Form["CanImplementInOtherFields"].ToString();
                var implementationArea = Request.Form["ImplementationArea"].ToString();

                // Validate that at least one field is filled (executive can fill only one field)
                bool hasAnyFieldFilled = false;
                
                if (!string.IsNullOrEmpty(approvedBy?.Trim()))
                {
                    hasAnyFieldFilled = true;
                }
                
                if (!string.IsNullOrEmpty(comments?.Trim()))
                {
                    hasAnyFieldFilled = true;
                }
                
                if (!string.IsNullOrEmpty(canImplementInOtherFields))
                {
                    hasAnyFieldFilled = true;
                }
                
                if (categories != null && categories.Length > 0)
                {
                    hasAnyFieldFilled = true;
                }
                
                if (!hasAnyFieldFilled)
                {
                    return Json(new { success = false, message = "Please fill in at least one field (Signature, Comments, Categories, or Implementation question)." });
                }

                // Update the kaizen with executive filling data
                kaizen.Category = categories != null ? string.Join(", ", categories) : "";
                kaizen.EngineerApprovedBy = approvedBy?.Trim();
                kaizen.Comments = comments?.Trim();
                kaizen.CanImplementInOtherFields = canImplementInOtherFields;
                kaizen.ImplementationArea = implementationArea?.Trim();
                
                // Preserve existing values for fields that should not be overwritten
                // These fields are not part of the executive filling form but should retain their values
                // DateImplemented and CostSaving should remain unchanged

                await _context.SaveChangesAsync();

                // Note: Manager email notification is now handled in UpdateEngineerStatus method
                // Only when engineer status is "Approved"

                        // Send email notifications to engineers in specified departments for inter-department implementation
        if (!string.IsNullOrEmpty(implementationArea?.Trim()) && 
            canImplementInOtherFields?.ToLower() == "yes")
        {
            Console.WriteLine($"=== EXECUTIVE FILLING - SENDING INTER-DEPARTMENT EMAILS ===");
            Console.WriteLine($"Implementation Area: '{implementationArea.Trim()}'");
            Console.WriteLine($"Can Implement in Other Fields: {canImplementInOtherFields}");
            
            await SendInterDepartmentEmailNotifications(kaizen, implementationArea.Trim());
            
            Console.WriteLine($"=== END EXECUTIVE FILLING - INTER-DEPARTMENT EMAILS ===");
        }
        else
        {
            Console.WriteLine($"=== EXECUTIVE FILLING - NO INTER-DEPARTMENT EMAILS ===");
            Console.WriteLine($"Implementation Area: '{implementationArea?.Trim()}'");
            Console.WriteLine($"Can Implement in Other Fields: {canImplementInOtherFields}");
            Console.WriteLine($"Skipping inter-department email notifications");
            Console.WriteLine($"=== END EXECUTIVE FILLING - NO INTER-DEPARTMENT EMAILS ===");
        }

                return Json(new { success = true, message = "Review saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/KaizenTeam - Full access page for kaizen team
        [HttpGet]
        public async Task<IActionResult> KaizenTeamDashboard()
        {
            // Check if user is kaizen team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Get statistics for dashboard
            var totalKaizens = _context.KaizenForms.Count();
            
            // Pending kaizens: where either engineer or manager is pending or null
            var pendingKaizens = _context.KaizenForms.Count(k => 
                k.EngineerStatus == "Pending" || k.ManagerStatus == "Pending" ||
                k.EngineerStatus == null || k.ManagerStatus == null);
            
            // Rejected kaizens: where either manager or engineer status is rejected
            var rejectedKaizens = _context.KaizenForms.Count(k => 
                k.EngineerStatus == "Rejected" || k.ManagerStatus == "Rejected");
            
            // Approved kaizens: where both manager and engineer are approved
            var approvedKaizens = _context.KaizenForms.Count(k => 
                k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

            // Awarded kaizens: where scores are assigned (dynamic award calculation)
            var awardedKaizens = _context.KaizenForms.Count(k => 
                k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && 
                _context.KaizenMarkingScores.Any(s => s.KaizenId == k.Id));

            // Calculate percentages
            var totalForPercentage = totalKaizens > 0 ? totalKaizens : 1;
            var pendingPercentage = Math.Round((double)pendingKaizens / totalForPercentage * 100, 1);
            var approvedPercentage = Math.Round((double)approvedKaizens / totalForPercentage * 100, 1);
            var rejectedPercentage = Math.Round((double)rejectedKaizens / totalForPercentage * 100, 1);

            // Get most active department
            var mostActiveDepartment = _context.KaizenForms
                .Where(k => k.Department != null && k.Department.Trim() != "")
                .GroupBy(k => k.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            // Get highest cost saving department
            var highestCostSavingDepartment = _context.KaizenForms
                .Where(k => k.Department != null && k.Department.Trim() != "" && k.CostSaving.HasValue && k.CostSaving > 0)
                .GroupBy(k => k.Department)
                .Select(g => new { Department = g.Key, TotalSaving = g.Sum(k => k.CostSaving.Value) })
                .OrderByDescending(x => x.TotalSaving)
                .FirstOrDefault();

            // Get highest cost saving amount
            var highestCostSaving = _context.KaizenForms
                .Where(k => k.CostSaving.HasValue && k.CostSaving > 0)
                .Max(k => k.CostSaving) ?? 0;

            ViewBag.TotalKaizens = totalKaizens;
            ViewBag.PendingKaizens = pendingKaizens;
            ViewBag.RejectedKaizens = rejectedKaizens;
            ViewBag.ApprovedKaizens = approvedKaizens;
            ViewBag.AwardedKaizens = awardedKaizens;
            ViewBag.PendingPercentage = pendingPercentage;
            ViewBag.ApprovedPercentage = approvedPercentage;
            ViewBag.RejectedPercentage = rejectedPercentage;
            ViewBag.MostActiveDepartment = mostActiveDepartment?.Department ?? "No Data";
            ViewBag.MostActiveCount = mostActiveDepartment?.Count ?? 0;
            ViewBag.HighestCostSavingDepartment = highestCostSavingDepartment?.Department ?? "No Data";
            ViewBag.HighestCostSaving = highestCostSaving;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> KaizenTeam(string searchString, string department, string status, 
            string startDate, string endDate, string category, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo, string quarter)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "kaizenteam" in their username
            if (!IsKaizenTeamRole())
            {
                // Redirect based on user role
                if (IsUserRole())
                {
                    return RedirectToAction("Kaizenform");
                }
                else if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard"); // Default fallback
                }
            }

            try
            {
                Console.WriteLine($"=== KAIZENTEAM DEBUG ===");
                Console.WriteLine($"KaizenTeam called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: {searchString}, Department: {department}, Status: {status}");
                Console.WriteLine($"StartDate: {startDate}, EndDate: {endDate}");

                var query = _context.KaizenForms.AsQueryable();

                // No department restrictions - show all kaizens
                Console.WriteLine("KaizenTeam: Showing all kaizens (no department restrictions)");

                // Apply search string filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower) ||
                        k.Department.ToLower().Contains(searchLower) ||
                        (k.SuggestionDescription != null && k.SuggestionDescription.ToLower().Contains(searchLower))
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply specific field filters
                if (!string.IsNullOrEmpty(kaizenNo))
                {
                    query = query.Where(k => k.KaizenNo.Contains(kaizenNo));
                }

                if (!string.IsNullOrEmpty(employeeName))
                {
                    query = query.Where(k => k.EmployeeName.ToLower().Contains(employeeName.ToLower()));
                }

                if (!string.IsNullOrEmpty(employeeNo))
                {
                    query = query.Where(k => k.EmployeeNo.ToLower().Contains(employeeNo.ToLower()));
                }

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                    Console.WriteLine($"Applied department filter: {department}");
                }


                // Engineer status and manager status filters removed as requested

                // Apply overall status filter
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Approved")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Approved" && 
                            (k.ManagerStatus ?? "Pending") == "Approved"
                        );
                    }
                    else if (status == "Rejected")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Rejected" || 
                            (k.ManagerStatus ?? "Pending") == "Rejected"
                        );
                    }
                    else if (status == "Pending")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") != "Rejected" && 
                            (k.ManagerStatus ?? "Pending") != "Rejected" &&
                            !((k.EngineerStatus ?? "Pending") == "Approved" && (k.ManagerStatus ?? "Pending") == "Approved")
                        );
                    }
                    Console.WriteLine($"Applied overall status filter: {status}");
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {start:yyyy-MM-dd}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                    Console.WriteLine($"Applied end date filter: {end:yyyy-MM-dd}");
                }

                // Apply cost saving range filter
                if (!string.IsNullOrEmpty(costSavingRange))
                {
                    switch (costSavingRange)
                    {
                        case "0-1000":
                            query = query.Where(k => k.CostSaving >= 0 && k.CostSaving <= 1000);
                            break;
                        case "1001-5000":
                            query = query.Where(k => k.CostSaving > 1000 && k.CostSaving <= 5000);
                            break;
                        case "5001-10000":
                            query = query.Where(k => k.CostSaving > 5000 && k.CostSaving <= 10000);
                            break;
                        case "10001+":
                            query = query.Where(k => k.CostSaving > 10000);
                            break;
                        case "No Cost Saving":
                            query = query.Where(k => k.CostSaving == null || k.CostSaving == 0);
                            break;
                    }
                }

                // Apply quarter filter
                if (!string.IsNullOrEmpty(quarter) && int.TryParse(quarter, out int quarterValue))
                {
                    var currentYear = DateTime.Now.Year;
                    var quarterStartMonth = ((quarterValue - 1) * 3) + 1;
                    var quarterStartDate = new DateTime(currentYear, quarterStartMonth, 1);
                    var quarterEndDate = quarterStartDate.AddMonths(3).AddDays(-1);
                    
                    query = query.Where(k => k.DateSubmitted >= quarterStartDate && k.DateSubmitted <= quarterEndDate);
                    Console.WriteLine($"Applied quarter filter: Q{quarterValue} ({quarterStartDate:yyyy-MM-dd} to {quarterEndDate:yyyy-MM-dd})");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenTeam returned {kaizens.Count} results");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    EngineerStatus: '{k.EngineerStatus}', ManagerStatus: '{k.ManagerStatus}'");
                    Console.WriteLine($"    DateSubmitted: {k.DateSubmitted:yyyy-MM-dd}");
                }
                
                Console.WriteLine($"=== END KAIZENTEAM DEBUG ===");

                // Get all unique departments for filter dropdown
                ViewBag.Departments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                // Get all unique categories
                var allCategories = new List<string>();
                var kaizensWithCategories = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Category))
                    .Select(k => k.Category)
                    .ToListAsync();

                foreach (var catString in kaizensWithCategories)
                {
                    if (!string.IsNullOrEmpty(catString))
                    {
                        var categories = catString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrEmpty(c));
                        allCategories.AddRange(categories);
                    }
                }

                ViewBag.Categories = allCategories.Distinct().OrderBy(c => c).ToList();

                // Pass current filter values back to view
                ViewBag.CurrentFilters = new
                {
                    SearchString = searchString,
                    Department = department,
                    Status = status,
                    StartDate = startDate,
                    EndDate = endDate,
                    Category = category,
                    CostSavingRange = costSavingRange,
                    EmployeeName = employeeName,
                    EmployeeNo = employeeNo,
                    KaizenNo = kaizenNo,
                    Quarter = quarter
                };

                return View("KaizenTeamView", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenTeam: {ex.Message}");
                return View("~/Views/Kaizen/KaizenTeam.cshtml", new List<KaizenForm>());
            }
        }

        // GET: /Kaizen/SearchTeam (AJAX endpoint for KaizenTeam search)
        [HttpGet]
        public async Task<IActionResult> SearchTeam(string searchString, string department, string status, 
            string startDate, string endDate, string category, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo, string quarter)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "kaizenteam" in their username
            if (!IsKaizenTeamRole())
            {
                return Json(new { success = false, message = "Access denied. Only kaizen team members can access this search." });
            }

            try
            {
                var query = _context.KaizenForms.AsQueryable();

                Console.WriteLine($"=== SEARCHTEAM DEBUG ===");
                Console.WriteLine($"SearchTeam called by: {User?.Identity?.Name}");
                Console.WriteLine($"SearchString: '{searchString}', Department: '{department}', Status: '{status}'");
                Console.WriteLine($"StartDate: '{startDate}', EndDate: '{endDate}'");

                // No department restrictions - show all kaizens
                Console.WriteLine("SearchTeam: Showing all kaizens (no department restrictions)");

                // Apply search string filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower) ||
                        k.Department.ToLower().Contains(searchLower) ||
                        (k.SuggestionDescription != null && k.SuggestionDescription.ToLower().Contains(searchLower))
                    );
                    Console.WriteLine($"Applied search filter for: {searchString}");
                }

                // Apply specific field filters
                if (!string.IsNullOrEmpty(kaizenNo))
                {
                    query = query.Where(k => k.KaizenNo.Contains(kaizenNo));
                }

                if (!string.IsNullOrEmpty(employeeName))
                {
                    query = query.Where(k => k.EmployeeName.ToLower().Contains(employeeName.ToLower()));
                }

                if (!string.IsNullOrEmpty(employeeNo))
                {
                    query = query.Where(k => k.EmployeeNo.ToLower().Contains(employeeNo.ToLower()));
                }

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                    Console.WriteLine($"Applied department filter: {department}");
                }


                // Engineer status and manager status filters removed as requested

                // Apply overall status filter
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Approved")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Approved" && 
                            (k.ManagerStatus ?? "Pending") == "Approved"
                        );
                        Console.WriteLine($"Applied approved filter: Both Engineer and Manager must be Approved");
                    }
                    else if (status == "Rejected")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Rejected" || 
                            (k.ManagerStatus ?? "Pending") == "Rejected"
                        );
                        Console.WriteLine($"Applied rejected filter: Either Engineer or Manager must be Rejected");
                    }
                    else if (status == "Pending")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") != "Rejected" && 
                            (k.ManagerStatus ?? "Pending") != "Rejected" &&
                            !((k.EngineerStatus ?? "Pending") == "Approved" && (k.ManagerStatus ?? "Pending") == "Approved")
                        );
                        Console.WriteLine($"Applied pending filter: Neither rejected and not both approved");
                    }
                    else
                    {
                        // Handle legacy status values for backward compatibility
                        if (status == "EngineerApproved" || status == "EngineerRejected" || status == "EngineerPending")
                        {
                            var legacyEngineerStatus = status.Replace("Engineer", "");
                            query = query.Where(k => (k.EngineerStatus ?? "Pending") == legacyEngineerStatus);
                            Console.WriteLine($"Applied engineer status filter: {legacyEngineerStatus}");
                        }
                        else if (status == "ManagerApproved" || status == "ManagerRejected" || status == "ManagerPending")
                        {
                            var legacyManagerStatus = status.Replace("Manager", "");
                            query = query.Where(k => (k.ManagerStatus ?? "Pending") == legacyManagerStatus);
                            Console.WriteLine($"Applied manager status filter: {legacyManagerStatus}");
                        }
                        else
                        {
                            // Default to EngineerStatus
                            query = query.Where(k => (k.EngineerStatus ?? "Pending") == status);
                            Console.WriteLine($"Applied default status filter: {status}");
                        }
                    }
                }

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {start:yyyy-MM-dd}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    query = query.Where(k => k.DateSubmitted <= end);
                    Console.WriteLine($"Applied end date filter: {end:yyyy-MM-dd}");
                }

                // Apply cost saving range filter
                if (!string.IsNullOrEmpty(costSavingRange))
                {
                    switch (costSavingRange)
                    {
                        case "0-1000":
                            query = query.Where(k => k.CostSaving >= 0 && k.CostSaving <= 1000);
                            break;
                        case "1001-5000":
                            query = query.Where(k => k.CostSaving > 1000 && k.CostSaving <= 5000);
                            break;
                        case "5001-10000":
                            query = query.Where(k => k.CostSaving > 5000 && k.CostSaving <= 10000);
                            break;
                        case "10001+":
                            query = query.Where(k => k.CostSaving > 10000);
                            break;
                        case "No Cost Saving":
                            query = query.Where(k => k.CostSaving == null || k.CostSaving == 0);
                            break;
                    }
                }

                // Apply quarter filter
                if (!string.IsNullOrEmpty(quarter) && int.TryParse(quarter, out int quarterValue))
                {
                    var currentYear = DateTime.Now.Year;
                    var quarterStartMonth = ((quarterValue - 1) * 3) + 1;
                    var quarterStartDate = new DateTime(currentYear, quarterStartMonth, 1);
                    var quarterEndDate = quarterStartDate.AddMonths(3).AddDays(-1);
                    
                    query = query.Where(k => k.DateSubmitted >= quarterStartDate && k.DateSubmitted <= quarterEndDate);
                    Console.WriteLine($"Applied quarter filter: Q{quarterValue} ({quarterStartDate:yyyy-MM-dd} to {quarterEndDate:yyyy-MM-dd})");
                }

                var kaizens = await query
                    .OrderByDescending(k => k.DateSubmitted)
                    .Select(k => new
                    {
                        id = k.Id,
                        kaizenNo = k.KaizenNo,
                        dateSubmitted = k.DateSubmitted.ToString("yyyy-MM-dd"),
                        department = k.Department,
                        employeeName = k.EmployeeName,
                        employeeNo = k.EmployeeNo,
                        employeePhotoPath = k.EmployeePhotoPath,
                        costSaving = k.CostSaving,
                        // Engineer and Manager approval fields
                        engineerStatus = k.EngineerStatus ?? "Pending",
                        engineerApprovedBy = k.EngineerApprovedBy,
                        managerStatus = k.ManagerStatus ?? "Pending",
                        managerApprovedBy = k.ManagerApprovedBy,
                        // Executive filling fields
                        category = k.Category,
                        comments = k.Comments,
                        canImplementInOtherFields = k.CanImplementInOtherFields,
                        implementationArea = k.ImplementationArea,
                        // Manager comment fields
                        managerComments = k.ManagerComments,
                        managerSignature = k.ManagerSignature,
                        // Additional fields for popup
                        otherBenefits = k.OtherBenefits,
                        beforeKaizenImagePath = k.BeforeKaizenImagePath,
                        afterKaizenImagePath = k.AfterKaizenImagePath
                    })
                    .ToListAsync();

                Console.WriteLine($"SearchTeam returned {kaizens.Count} results");
                Console.WriteLine($"=== END SEARCHTEAM DEBUG ===");

                return Json(new { success = true, kaizens = kaizens });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchTeam error: {ex.Message}");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/GetCategories - Get all active categories for dropdowns
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new { id = c.Id, name = c.Name })
                    .ToListAsync();

                return Json(new { success = true, categories = categories });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: /Kaizen/DepartmentTargets - Read-only department targets for kaizen team
        [HttpGet]
        public async Task<IActionResult> DepartmentTargets(int? year, int? month, string departmentSearch)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "kaizenteam" in their username
            if (!IsKaizenTeamRole())
            {
                // Redirect based on user role
                if (IsUserRole())
                {
                    return RedirectToAction("Kaizenform");
                }
                else if (IsManagerRole())
                {
                    return RedirectToAction("KaizenListManager");
                }
                else if (IsEngineerRole())
                {
                    return RedirectToAction("EngineerDashboard");
                }
                else
                {
                    return RedirectToAction("EngineerDashboard"); // Default fallback
                }
            }

            try
            {
                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;

                var viewModel = new DepartmentTargetsPageViewModel
                {
                    SelectedYear = selectedYear,
                    SelectedMonth = selectedMonth,
                    AvailableYears = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).ToList(),
                    AvailableMonths = Enumerable.Range(1, 12).ToList()
                };

                // Get all departments that have targets for the selected month/year
                var departmentTargets = await _context.DepartmentTargets
                    .Where(dt => dt.Year == selectedYear && dt.Month == selectedMonth)
                    .ToListAsync();

                // Get all departments from kaizen forms
                var allDepartments = await _context.KaizenForms
                    .Where(k => k.Department != null && k.Department.Trim() != "")
                    .Select(k => k.Department)
                    .Distinct()
                    .ToListAsync();

                foreach (var department in allDepartments)
                {
                    var target = departmentTargets.FirstOrDefault(dt => dt.Department == department);
                    var targetCount = target?.TargetCount ?? 0;

                    // Count achieved kaizens for this department in the selected month/year
                    var achievedCount = await _context.KaizenForms
                        .CountAsync(k => k.Department == department && 
                                        k.DateSubmitted.Year == selectedYear && 
                                        k.DateSubmitted.Month == selectedMonth);

                    viewModel.DepartmentTargets.Add(new DepartmentTargetViewModel
                    {
                        Department = department,
                        TargetCount = targetCount,
                        AchievedCount = achievedCount,
                        Year = selectedYear,
                        Month = selectedMonth
                    });
                }

                // Filter by department if provided
                if (!string.IsNullOrEmpty(departmentSearch))
                {
                    viewModel.DepartmentTargets = viewModel.DepartmentTargets
                        .Where(dt => dt.Department.Equals(departmentSearch, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Calculate totals
                viewModel.TotalTarget = viewModel.DepartmentTargets.Sum(dt => dt.TargetCount);
                viewModel.TotalAchieved = viewModel.DepartmentTargets.Sum(dt => dt.AchievedCount);

                // Calculate most submitted department
                var departmentSubmissions = await _context.KaizenForms
                    .Where(k => k.Department != null && k.Department.Trim() != "")
                    .GroupBy(k => k.Department)
                    .Select(g => new { Department = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefaultAsync();

                // Calculate most cost saving department
                var costSavingDepartments = await _context.KaizenForms
                    .Where(k => k.Department != null && k.Department.Trim() != "" && k.CostSaving.HasValue && k.CostSaving > 0)
                    .GroupBy(k => k.Department)
                    .Select(g => new { Department = g.Key, TotalSaving = g.Sum(k => k.CostSaving!.Value) })
                    .OrderByDescending(x => x.TotalSaving)
                    .FirstOrDefaultAsync();

                ViewBag.MostSubmittedDepartment = departmentSubmissions?.Department ?? "No Data";
                ViewBag.MostSubmittedCount = departmentSubmissions?.Count ?? 0;
                ViewBag.MostCostSavingDepartment = costSavingDepartments?.Department ?? "No Data";
                ViewBag.MostCostSavingAmount = costSavingDepartments?.TotalSaving ?? 0;

                // Calculate most cost saving individual kaizen
                var mostCostSavingKaizen = await _context.KaizenForms
                    .Where(k => k.CostSaving.HasValue && k.CostSaving > 0)
                    .OrderByDescending(k => k.CostSaving)
                    .Select(k => new { 
                        KaizenNo = k.KaizenNo, 
                        CostSavingAmount = k.CostSaving!.Value, 
                        Department = k.Department 
                    })
                    .FirstOrDefaultAsync();

                ViewBag.MostCostSavingKaizenNo = mostCostSavingKaizen?.KaizenNo ?? "No Data";
                ViewBag.MostCostSavingKaizenAmount = mostCostSavingKaizen?.CostSavingAmount ?? 0;
                ViewBag.MostCostSavingKaizenDepartment = mostCostSavingKaizen?.Department ?? "No Data";

                return View("~/Views/Kaizen/DepartmentTargets.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DepartmentTargets: {ex.Message}");
                return View("~/Views/Kaizen/DepartmentTargets.cshtml", new DepartmentTargetsPageViewModel());
            }
        }

        // GET: /Kaizen/DepartmentTargetsManager - Department targets for managers (shows only their department)
        [HttpGet]
        public async Task<IActionResult> DepartmentTargetsManager(int? year, int? month)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;

                // Get current manager's department
                var managerDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(managerDepartment))
                {
                    TempData["AlertMessage"] = "Unable to determine your department. Please contact administrator.";
                    return RedirectToAction("KaizenListManager");
                }

                var viewModel = new DepartmentTargetsPageViewModel
                {
                    SelectedYear = selectedYear,
                    SelectedMonth = selectedMonth,
                    AvailableYears = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).ToList(),
                    AvailableMonths = Enumerable.Range(1, 12).ToList()
                };

                // Get department target for the manager's department
                var departmentTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == managerDepartment && dt.Year == selectedYear && dt.Month == selectedMonth)
                    .FirstOrDefaultAsync();

                var targetCount = departmentTarget?.TargetCount ?? 0;

                // Count achieved kaizens for this department in the selected month/year
                var achievedCount = await _context.KaizenForms
                    .CountAsync(k => k.Department == managerDepartment && 
                                    k.DateSubmitted.Year == selectedYear && 
                                    k.DateSubmitted.Month == selectedMonth);

                viewModel.DepartmentTargets.Add(new DepartmentTargetViewModel
                {
                    Department = managerDepartment,
                    TargetCount = targetCount,
                    AchievedCount = achievedCount,
                    Year = selectedYear,
                    Month = selectedMonth
                });

                // Calculate totals
                viewModel.TotalTarget = viewModel.DepartmentTargets.Sum(dt => dt.TargetCount);
                viewModel.TotalAchieved = viewModel.DepartmentTargets.Sum(dt => dt.AchievedCount);

                // Calculate department-specific statistics
                var departmentSubmissions = await _context.KaizenForms
                    .Where(k => k.Department == managerDepartment)
                    .CountAsync();

                var costSavingTotal = await _context.KaizenForms
                    .Where(k => k.Department == managerDepartment && k.CostSaving.HasValue && k.CostSaving > 0)
                    .SumAsync(k => k.CostSaving!.Value);

                var mostCostSavingKaizen = await _context.KaizenForms
                    .Where(k => k.Department == managerDepartment && k.CostSaving.HasValue && k.CostSaving > 0)
                    .OrderByDescending(k => k.CostSaving)
                    .Select(k => new { 
                        KaizenNo = k.KaizenNo, 
                        CostSavingAmount = k.CostSaving!.Value 
                    })
                    .FirstOrDefaultAsync();

                ViewBag.MostSubmittedDepartment = managerDepartment;
                ViewBag.MostSubmittedCount = departmentSubmissions;
                ViewBag.MostCostSavingDepartment = managerDepartment;
                ViewBag.MostCostSavingAmount = costSavingTotal;
                ViewBag.MostCostSavingKaizenNo = mostCostSavingKaizen?.KaizenNo ?? "No Data";
                ViewBag.MostCostSavingKaizenAmount = mostCostSavingKaizen?.CostSavingAmount ?? 0;

                return View("~/Views/Kaizen/DepartmentTargetsManager.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DepartmentTargetsManager: {ex.Message}");
                TempData["AlertMessage"] = "An error occurred while loading department targets.";
                return RedirectToAction("KaizenListManager");
            }
        }

        // GET: /Kaizen/KaizenDetails/{id} - Manager kaizen details page
        [HttpGet]
        public async Task<IActionResult> KaizenDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    TempData["ErrorMessage"] = "Kaizen suggestion not found.";
                    return RedirectToAction("KaizenListManager");
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                return View("~/Views/Kaizen/KaizenDetails.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the kaizen details.";
                return RedirectToAction("KaizenListManager");
            }
        }

        // GET: /Kaizen/KaizenTeamDetails - Kaizen Team details page
        [HttpGet]
        public async Task<IActionResult> KaizenTeamDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow kaizen team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    TempData["ErrorMessage"] = "Kaizen suggestion not found.";
                    return RedirectToAction("KaizenTeamView");
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                return View("~/Views/Kaizen/KaizenTeamDetails.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenTeamDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the kaizen details.";
                return RedirectToAction("KaizenTeamView");
            }
        }

        // GET: /Kaizen/ManagerDashboard - Manager dashboard with summary boxes
        [HttpGet]
        public async Task<IActionResult> ManagerDashboard()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    return RedirectToAction("Kaizenform");
                }

                // Get current month and year
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get department kaizens for current month (all kaizens without any restrictions)
                var currentMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == currentYear && 
                               k.DateSubmitted.Month == currentMonth)
                    .ToListAsync();

                // Get department kaizens for previous month
                var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
                var previousMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == previousYear && 
                               k.DateSubmitted.Month == previousMonth)
                    .ToListAsync();

                // Get department target for current month
                var currentMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == currentYear && 
                                dt.Month == currentMonth)
                    .FirstOrDefaultAsync();

                // Calculate summary statistics
                var currentMonthSubmissions = currentMonthKaizens.Count;
                var currentMonthTargetCount = currentMonthTarget?.TargetCount ?? 0;
                var currentMonthAchievement = currentMonthTargetCount > 0 ? 
                    (double)currentMonthSubmissions / currentMonthTargetCount * 100 : 0;

                var previousMonthSubmissions = previousMonthKaizens.Count;
                var previousMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == previousYear && 
                                dt.Month == previousMonth)
                    .FirstOrDefaultAsync();
                var previousMonthTargetCount = previousMonthTarget?.TargetCount ?? 0;
                var previousMonthAchievement = previousMonthTargetCount > 0 ? 
                    (double)previousMonthSubmissions / previousMonthTargetCount * 100 : 0;

                // Calculate cost savings (only from approved kaizens)
                var currentMonthCostSavings = currentMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved")
                    .Sum(k => k.CostSaving ?? 0);
                var previousMonthCostSavings = previousMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved")
                    .Sum(k => k.CostSaving ?? 0);

                // Calculate pending approvals - kaizens with engineer approval but manager approval is null
                var pendingApprovals = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.EngineerStatus == "Approved" && 
                               k.ManagerStatus == null)
                    .CountAsync();

                // Calculate completed kaizens
                var completedKaizens = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved");

                // Calculate manager-specific statistics for Review Completion
                // Count kaizens where engineer status is "Approved" and manager status is either "Approved" or "Rejected"
                var engineerApprovedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved");
                var approvedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");
                var rejectedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Rejected");
                var totalReviewed = approvedKaizens + rejectedKaizens;
                
                // Calculate kaizens with both manager and engineer status as "Approved"
                var bothApprovedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

                // Create dashboard view model
                var dashboardViewModel = new ManagerDashboardViewModel
                {
                    CurrentMonthSubmissions = currentMonthSubmissions,
                    CurrentMonthTarget = currentMonthTargetCount,
                    CurrentMonthAchievement = currentMonthAchievement,
                    PreviousMonthSubmissions = previousMonthSubmissions,
                    PreviousMonthTarget = previousMonthTargetCount,
                    PreviousMonthAchievement = previousMonthAchievement,
                    CurrentMonthCostSavings = currentMonthCostSavings,
                    PreviousMonthCostSavings = previousMonthCostSavings,
                    PendingApprovals = pendingApprovals,
                    CompletedKaizens = completedKaizens,
                    EngineerApprovedKaizens = engineerApprovedKaizens,
                    ApprovedKaizens = approvedKaizens,
                    RejectedKaizens = rejectedKaizens,
                    TotalReviewed = totalReviewed,
                    BothApprovedKaizens = bothApprovedKaizens,
                    Department = currentUserDepartment,
                    CurrentMonth = currentMonth,
                    CurrentYear = currentYear
                };

                return View("~/Views/Kaizen/ManagerDashboard.cshtml", dashboardViewModel);
            }
            catch (Exception)
            {
                // Log the exception
                return RedirectToAction("Kaizenform");
            }
        }

        // GET: /Kaizen/EngineerDashboard - Engineer dashboard with summary boxes
        [HttpGet]
        public async Task<IActionResult> EngineerDashboard()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers
            if (!IsEngineerRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    return RedirectToAction("Kaizenform");
                }

                // Get current month and year
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get department kaizens for current month
                var currentMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == currentYear && 
                               k.DateSubmitted.Month == currentMonth)
                    .ToListAsync();

                // Get department kaizens for previous month
                var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
                var previousMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == previousYear && 
                               k.DateSubmitted.Month == previousMonth)
                    .ToListAsync();

                // Get department target for current month
                var currentMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == currentYear && 
                                dt.Month == currentMonth)
                    .FirstOrDefaultAsync();

                // Calculate summary statistics
                var currentMonthSubmissions = currentMonthKaizens.Count;
                var currentMonthTargetCount = currentMonthTarget?.TargetCount ?? 0;
                var currentMonthAchievement = currentMonthTargetCount > 0 ? 
                    (double)currentMonthSubmissions / currentMonthTargetCount * 100 : 0;

                var previousMonthSubmissions = previousMonthKaizens.Count;
                var previousMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == previousYear && 
                                dt.Month == previousMonth)
                    .FirstOrDefaultAsync();
                var previousMonthTargetCount = previousMonthTarget?.TargetCount ?? 0;
                var previousMonthAchievement = previousMonthTargetCount > 0 ? 
                    (double)previousMonthSubmissions / previousMonthTargetCount * 100 : 0;

                // Calculate cost savings (only from engineer approved kaizens with cost savings)
                var currentMonthCostSavings = currentMonthKaizens
                    .Where(k => k.EngineerStatus == "Approved" && k.CostSaving.HasValue && k.CostSaving > 0)
                    .Sum(k => k.CostSaving.Value);
                var previousMonthCostSavings = previousMonthKaizens
                    .Where(k => k.EngineerStatus == "Approved" && k.CostSaving.HasValue && k.CostSaving > 0)
                    .Sum(k => k.CostSaving.Value);

                // Calculate engineer-specific statistics
                var pendingReviews = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Pending" || k.EngineerStatus == null);
                var approvedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved");
                var rejectedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Rejected");
                var totalReviewed = approvedKaizens + rejectedKaizens;
                
                // Calculate fully implemented kaizens (those with DateImplemented not null)
                var fullyImplementedKaizens = currentMonthKaizens.Count(k => 
                    k.DateImplemented.HasValue);
                
                // Calculate kaizens with both manager and engineer status as "Approved"
                var bothApprovedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

                // Create dashboard view model
                var dashboardViewModel = new EngineerDashboardViewModel
                {
                    CurrentMonthSubmissions = currentMonthSubmissions,
                    CurrentMonthTarget = currentMonthTargetCount,
                    CurrentMonthAchievement = currentMonthAchievement,
                    PreviousMonthSubmissions = previousMonthSubmissions,
                    PreviousMonthTarget = previousMonthTargetCount,
                    PreviousMonthAchievement = previousMonthAchievement,
                    CurrentMonthCostSavings = currentMonthCostSavings,
                    PreviousMonthCostSavings = previousMonthCostSavings,
                    PendingReviews = pendingReviews,
                    ApprovedKaizens = approvedKaizens,
                    RejectedKaizens = rejectedKaizens,
                    TotalReviewed = totalReviewed,
                    FullyImplementedKaizens = fullyImplementedKaizens,
                    BothApprovedKaizens = bothApprovedKaizens,
                    Department = currentUserDepartment,
                    CurrentMonth = currentMonth,
                    CurrentYear = currentYear
                };

                return View("~/Views/Kaizen/EngineerDashboard.cshtml", dashboardViewModel);
            }
            catch (Exception)
            {
                // Log the exception
                return RedirectToAction("Kaizenform");
            }
        }

        // GET: /Kaizen/UserDashboard - User dashboard with summary boxes
        [HttpGet]
        public async Task<IActionResult> UserDashboard()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users (users with "user" in their username)
            if (!IsUserRole())
            {
                return RedirectToAction("Kaizenform");
            }

            try
            {
                // Get current user information
                var username = User?.Identity?.Name;
                var currentUser = await GetCurrentUserAsync();
                var currentUserDepartment = await GetCurrentUserDepartment();

                if (currentUser == null || string.IsNullOrEmpty(currentUserDepartment))
                {
                    Console.WriteLine($"UserDashboard - User not found or no department. Username: {username}, CurrentUser: {currentUser?.UserName}, Department: {currentUserDepartment}");
                    return RedirectToAction("Kaizenform");
                }

                // Get current month and year
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get user's kaizens for current month
                var currentMonthKaizens = await _context.KaizenForms
                    .Where(k => k.EmployeeNo == currentUser.EmployeeNumber && 
                               k.DateSubmitted.Year == currentYear && 
                               k.DateSubmitted.Month == currentMonth)
                    .ToListAsync();

                // Get user's kaizens for previous month
                var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
                var previousMonthKaizens = await _context.KaizenForms
                    .Where(k => k.EmployeeNo == currentUser.EmployeeNumber && 
                               k.DateSubmitted.Year == previousYear && 
                               k.DateSubmitted.Month == previousMonth)
                    .ToListAsync();

                // Get user's total kaizens (all time)
                var totalKaizensSubmitted = await _context.KaizenForms
                    .Where(k => k.EmployeeNo == currentUser.EmployeeNumber)
                    .CountAsync();

                // Get department target for current month
                var currentMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == currentYear && 
                                dt.Month == currentMonth)
                    .FirstOrDefaultAsync();

                // Get department total submissions for current month
                var departmentCurrentMonthSubmissions = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == currentYear && 
                               k.DateSubmitted.Month == currentMonth)
                    .CountAsync();

                // Calculate department target achievement
                var departmentTargetCount = currentMonthTarget?.TargetCount ?? 0;
                var departmentTargetAchievement = departmentTargetCount > 0 ? 
                    (double)departmentCurrentMonthSubmissions / departmentTargetCount * 100 : 0;

                // Calculate user's kaizen status statistics
                var pendingKaizens = currentMonthKaizens.Count(k => 
                    (k.EngineerStatus == null || k.EngineerStatus == "Pending") || 
                    (k.EngineerStatus == "Approved" && (k.ManagerStatus == null || k.ManagerStatus == "Pending")));
                var approvedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");
                var rejectedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Rejected" || k.ManagerStatus == "Rejected");
                var totalReviewed = approvedKaizens + rejectedKaizens;

                // Calculate cost savings
                var totalCostSavings = await _context.KaizenForms
                    .Where(k => k.EmployeeNo == currentUser.EmployeeNumber && 
                               k.CostSaving.HasValue && k.CostSaving > 0)
                    .SumAsync(k => k.CostSaving.Value);
                var currentMonthCostSavings = currentMonthKaizens
                    .Where(k => k.CostSaving.HasValue && k.CostSaving > 0)
                    .Sum(k => k.CostSaving.Value);

                // Calculate additional statistics for enhanced dashboard
                var fullyImplementedKaizens = currentMonthKaizens.Count(k => 
                    k.DateImplemented.HasValue);
                var bothApprovedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");
                
                // UserContributionToDepartment is now a computed property in the view model

                // Get previous month target for comparison
                var previousMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == previousYear && 
                                dt.Month == previousMonth)
                    .FirstOrDefaultAsync();
                var previousMonthTargetCount = previousMonthTarget?.TargetCount ?? 0;
                var previousMonthAchievement = previousMonthTargetCount > 0 ? 
                    (double)previousMonthKaizens.Count / previousMonthTargetCount * 100 : 0;

                // Create dashboard view model
                var dashboardViewModel = new UserDashboardViewModel
                {
                    TotalKaizensSubmitted = totalKaizensSubmitted,
                    CurrentMonthSubmissions = currentMonthKaizens.Count,
                    PreviousMonthSubmissions = previousMonthKaizens.Count,
                    DepartmentTarget = departmentTargetCount,
                    DepartmentCurrentMonthSubmissions = departmentCurrentMonthSubmissions,
                    DepartmentTargetAchievement = departmentTargetAchievement,
                    PendingKaizens = pendingKaizens,
                    ApprovedKaizens = approvedKaizens,
                    RejectedKaizens = rejectedKaizens,
                    TotalReviewed = totalReviewed,
                    TotalCostSavings = totalCostSavings,
                    CurrentMonthCostSavings = currentMonthCostSavings,
                    EmployeeName = currentUser.EmployeeName ?? username,
                    EmployeeNumber = currentUser.EmployeeNumber,
                    Department = currentUserDepartment,
                    CurrentMonth = currentMonth,
                    CurrentYear = currentYear,
                    // Additional properties for enhanced dashboard
                    FullyImplementedKaizens = fullyImplementedKaizens,
                    BothApprovedKaizens = bothApprovedKaizens,
                    PreviousMonthTarget = previousMonthTargetCount,
                    PreviousMonthAchievement = previousMonthAchievement
                };

                return View("~/Views/Kaizen/UserDashboard.cshtml", dashboardViewModel);
            }
            catch (Exception)
            {
                // Log the exception
                return RedirectToAction("Kaizenform");
            }
        }

        // GET: /Kaizen/SupervisorDashboard - Supervisor dashboard
        [HttpGet]
        public async Task<IActionResult> SupervisorDashboard()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    return RedirectToAction("Kaizenform");
                }

                // Get current month and year
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get department kaizens for current month
                var currentMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == currentYear && 
                               k.DateSubmitted.Month == currentMonth)
                    .ToListAsync();

                // Get department kaizens for previous month
                var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
                var previousMonthKaizens = await _context.KaizenForms
                    .Where(k => k.Department == currentUserDepartment && 
                               k.DateSubmitted.Year == previousYear && 
                               k.DateSubmitted.Month == previousMonth)
                    .ToListAsync();

                // Get department target for current month
                var currentMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == currentYear && 
                                dt.Month == currentMonth)
                    .FirstOrDefaultAsync();

                // Calculate summary statistics
                var currentMonthSubmissions = currentMonthKaizens.Count;
                var currentMonthTargetCount = currentMonthTarget?.TargetCount ?? 0;
                var currentMonthAchievement = currentMonthTargetCount > 0 ? 
                    (double)currentMonthSubmissions / currentMonthTargetCount * 100 : 0;

                var previousMonthSubmissions = previousMonthKaizens.Count;
                var previousMonthTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == currentUserDepartment && 
                                dt.Year == previousYear && 
                                dt.Month == previousMonth)
                    .FirstOrDefaultAsync();
                var previousMonthTargetCount = previousMonthTarget?.TargetCount ?? 0;
                var previousMonthAchievement = previousMonthTargetCount > 0 ? 
                    (double)previousMonthSubmissions / previousMonthTargetCount * 100 : 0;

                // Calculate cost savings (only from supervisor approved kaizens with cost savings)
                var currentMonthCostSavings = currentMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.CostSaving.HasValue && k.CostSaving > 0)
                    .Sum(k => k.CostSaving.Value);
                var previousMonthCostSavings = previousMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.CostSaving.HasValue && k.CostSaving > 0)
                    .Sum(k => k.CostSaving.Value);

                // Calculate supervisor-specific statistics
                var pendingReviews = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Pending" || k.ManagerStatus == null);
                var approvedKaizens = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Approved");
                var rejectedKaizens = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Rejected");
                var totalReviewed = approvedKaizens + rejectedKaizens;
                
                // Calculate fully implemented kaizens (those with DateImplemented not null)
                var fullyImplementedKaizens = currentMonthKaizens.Count(k => 
                    k.DateImplemented.HasValue);
                
                // Calculate kaizens with both manager and engineer status as "Approved"
                var bothApprovedKaizens = currentMonthKaizens.Count(k => 
                    k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

                // Get team members count (users in the same department)
                var teamMembersCount = await _context.Users
                    .Where(u => u.DepartmentName == currentUserDepartment)
                    .CountAsync();

                // Create dashboard view model
                var dashboardViewModel = new SupervisorDashboardViewModel
                {
                    CurrentMonthSubmissions = currentMonthSubmissions,
                    CurrentMonthTarget = currentMonthTargetCount,
                    CurrentMonthAchievement = currentMonthAchievement,
                    PreviousMonthSubmissions = previousMonthSubmissions,
                    PreviousMonthTarget = previousMonthTargetCount,
                    PreviousMonthAchievement = previousMonthAchievement,
                    CurrentMonthCostSavings = currentMonthCostSavings,
                    PreviousMonthCostSavings = previousMonthCostSavings,
                    PendingReviews = pendingReviews,
                    ApprovedKaizens = approvedKaizens,
                    RejectedKaizens = rejectedKaizens,
                    TotalReviewed = totalReviewed,
                    FullyImplementedKaizens = fullyImplementedKaizens,
                    BothApprovedKaizens = bothApprovedKaizens,
                    TeamMembersCount = teamMembersCount,
                    Department = currentUserDepartment,
                    CurrentMonth = currentMonth,
                    CurrentYear = currentYear
                };

                return View("~/Views/Kaizen/SupervisorDashboard.cshtml", dashboardViewModel);
            }
            catch (Exception)
            {
                // Log the exception
                return RedirectToAction("Kaizenform");
            }
        }



        // GET: /Kaizen/AwardTrackingManager - Award tracking for managers (department-specific)
        [HttpGet]
        public async Task<IActionResult> AwardTrackingManager(string startDate, string endDate, string category, string awardStatus)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    TempData["Error"] = "Unable to determine your department. Please contact administrator.";
                    return RedirectToAction("KaizenListManager");
                }

                // Get base query for approved kaizens in manager's department only
                var query = _context.KaizenForms
                    .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && k.Department == currentUserDepartment);

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                }

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                }

                // Note: Award status filtering will be applied after dynamic calculation
                // since we need to calculate awards based on scores, not static AwardPrice field

                // Get filtered results
                var approvedKaizens = query.ToList();

                // Calculate scores for each kaizen (same logic as Admin)
                var kaizensWithScores = new List<object>();
                foreach (var kaizen in approvedKaizens)
                {
                    var scores = _context.KaizenMarkingScores.Where(s => s.KaizenId == kaizen.Id).ToList();
                    
                    // Get the total weight of criteria that were actually scored for this kaizen
                    var scoredCriteriaIds = scores.Select(s => s.MarkingCriteriaId).ToList();
                    var totalWeight = _context.MarkingCriteria
                        .Where(c => scoredCriteriaIds.Contains(c.Id))
                        .Sum(c => c.Weight);
                    
                    var totalScore = scores.Sum(s => s.Score);
                    var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;

                    // Get award information using dynamic calculation (same as Admin)
                    var awardInfo = GetAwardForPercentage(percentage);
                    var awardName = awardInfo.AwardName;
                    var awardClass = awardInfo.AwardClass;

                    kaizensWithScores.Add(new
                    {
                        Kaizen = kaizen,
                        Score = totalScore,
                        TotalWeight = totalWeight,
                        Percentage = percentage,
                        AwardName = awardName,
                        AwardClass = awardClass
                    });
                }

                // Apply award status filtering after dynamic calculation
                if (!string.IsNullOrEmpty(awardStatus))
                {
                    if (awardStatus == "Pending")
                    {
                        // Filter for kaizens that don't have scores yet
                        kaizensWithScores = kaizensWithScores.Where(k => ((dynamic)k).TotalWeight == 0).ToList();
                    }
                    else if (awardStatus == "Assigned")
                    {
                        // Filter for kaizens that have scores (any award assigned)
                        kaizensWithScores = kaizensWithScores.Where(k => ((dynamic)k).TotalWeight > 0).ToList();
                    }
                    else
                    {
                        // Filter for kaizens with the specific award name
                        kaizensWithScores = kaizensWithScores.Where(k => ((dynamic)k).AwardName == awardStatus).ToList();
                    }
                }

                // Sort by percentage in descending order (highest score first)
                kaizensWithScores = kaizensWithScores
                    .OrderByDescending(k => ((dynamic)k).Percentage)
                    .ThenByDescending(k => ((dynamic)k).Score)
                    .ThenByDescending(k => ((dynamic)k).Kaizen.DateSubmitted)
                    .ToList();

                // Populate ViewBag with filter options for manager's department only
                var allCategories = new List<string>();
                var kaizensWithCategories = _context.KaizenForms
                    .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && 
                                k.Department == currentUserDepartment && !string.IsNullOrEmpty(k.Category))
                    .Select(k => k.Category)
                    .ToList();

                foreach (var catString in kaizensWithCategories)
                {
                    if (!string.IsNullOrEmpty(catString))
                    {
                        var categories = catString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrEmpty(c));
                        allCategories.AddRange(categories);
                    }
                }

                ViewBag.Categories = allCategories.Distinct().OrderBy(c => c).ToList();

                // Get dynamic award names from AwardThresholds table for filter dropdown
                var awardNames = _context.AwardThresholds
                    .Where(t => t.IsActive)
                    .Select(t => t.AwardName)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList();
                
                // Add "Pending" and "Assigned" options
                var allAwardStatusOptions = new List<string> { "Pending", "Assigned" };
                allAwardStatusOptions.AddRange(awardNames);
                ViewBag.AwardStatusOptions = allAwardStatusOptions;

                return View("AwardTrackingManager", kaizensWithScores);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AwardTrackingManager: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/AwardDetailsManager - Award details for managers
        [HttpGet]
        public async Task<IActionResult> AwardDetailsManager(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    TempData["Error"] = "Unable to determine your department. Please contact administrator.";
                    return RedirectToAction("AwardTrackingManager");
                }

                var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == id && k.Department == currentUserDepartment);
                if (kaizen == null)
                {
                    return NotFound();
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Get award thresholds
                var awardThresholds = await _context.AwardThresholds
                    .Where(t => t.IsActive)
                    .ToListAsync();

                // Pass data to view via ViewBag
                ViewBag.ExistingScores = existingScores;
                ViewBag.MarkingCriteria = markingCriteria;
                ViewBag.AwardThresholds = awardThresholds;

                return View("AwardDetailsManager", kaizen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AwardDetailsManager: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/AwardDetails - Award details for Kaizen Team (Read-Only)
        [HttpGet]
        public async Task<IActionResult> AwardDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow Kaizen Team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == id);
                if (kaizen == null)
                {
                    return NotFound();
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores
                };

                return View("AwardDetails", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AwardDetails: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // POST: /Kaizen/AssignAwardManager - Assign award for managers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAward(int kaizenId, string awardPrice, string committeeComments, string committeeSignature)
        {
            // Check if user is kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower().Contains("kaizenteam") != true)
            {
                return Json(new { success = false, message = "Access denied. Only kaizen team members can assign awards." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(kaizenId);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Award price is now calculated dynamically based on scores
                kaizen.CommitteeComments = committeeComments;
                kaizen.CommitteeSignature = committeeSignature;
                kaizen.AwardDate = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Award assigned successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAwardManager(int kaizenId, string awardPrice, string committeeComments, string committeeSignature)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers
            if (!IsManagerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Get current user's department
                var currentUserDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(currentUserDepartment))
                {
                    TempData["Error"] = "Unable to determine your department. Please contact administrator.";
                    return RedirectToAction("AwardTrackingManager");
                }

                var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == kaizenId && k.Department == currentUserDepartment);
                if (kaizen == null)
                {
                    return NotFound();
                }

                // Award price is now calculated dynamically based on scores
                kaizen.CommitteeComments = committeeComments;
                kaizen.CommitteeSignature = committeeSignature;
                kaizen.AwardDate = DateTime.Now;

                _context.SaveChanges();

                TempData["SubmissionSuccessMessage"] = "Award assigned successfully!";
                return RedirectToAction("AwardTrackingManager");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AssignAwardManager: {ex.Message}");
                TempData["Error"] = "An error occurred while assigning the award.";
                return RedirectToAction("AwardTrackingManager");
            }
        }

        // GET: /Kaizen/AboutSystem
        [HttpGet]
        public async Task<IActionResult> AboutSystem()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Allow kaizen team and managers
            if (!IsKaizenTeamRole() && !IsManagerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AboutSystem: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/SupervisorAboutSystem
        [HttpGet]
        public async Task<IActionResult> SupervisorAboutSystem()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow supervisors
            if (!IsSupervisorRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SupervisorAboutSystem: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/EngineerAboutSystem
        [HttpGet]
        public async Task<IActionResult> EngineerAboutSystem()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers
            if (!IsEngineerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EngineerAboutSystem: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/UserAboutSystem
        [HttpGet]
        public async Task<IActionResult> UserAboutSystem()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "user" in their username
            if (!IsUserRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserAboutSystem: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/UserManagement
        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow kaizen team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var users = _context.Users.ToList();
                return View(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserManagement: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> KaizenDetailsEngineer(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers
            if (!IsEngineerRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    TempData["ErrorMessage"] = "Kaizen suggestion not found.";
                    return RedirectToAction("KaizenListEngineer");
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                return View("~/Views/Kaizen/KaizenDetailsEngineer.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KaizenDetailsEngineer: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the kaizen details.";
                return RedirectToAction("KaizenListEngineer");
            }
        }

        // GET: /Kaizen/UserKaizenDetails
        [HttpGet]
        public async Task<IActionResult> UserKaizenDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var kaizen = await _context.KaizenForms
                    .FirstOrDefaultAsync(k => k.Id == id);

                if (kaizen == null)
                {
                    return NotFound();
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                return View("~/Views/Kaizen/UserKaizenDetails.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserKaizenDetails: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/ExportToExcel
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string searchString, string department, string status,
            string startDate, string endDate, string category,
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo, string quarter)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow users with "kaizenteam" in their username
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var query = _context.KaizenForms.AsQueryable();

                // Apply the same filters as KaizenTeam action
                if (!string.IsNullOrEmpty(searchString))
                {
                    var searchLower = searchString.ToLower();
                    query = query.Where(k => 
                        k.KaizenNo.ToLower().Contains(searchLower) ||
                        k.EmployeeName.ToLower().Contains(searchLower) ||
                        k.EmployeeNo.ToLower().Contains(searchLower) ||
                        k.Department.ToLower().Contains(searchLower) ||
                        (k.SuggestionDescription != null && k.SuggestionDescription.ToLower().Contains(searchLower))
                    );
                }

                if (!string.IsNullOrEmpty(kaizenNo))
                {
                    query = query.Where(k => k.KaizenNo.Contains(kaizenNo));
                }

                if (!string.IsNullOrEmpty(employeeName))
                {
                    query = query.Where(k => k.EmployeeName.ToLower().Contains(employeeName.ToLower()));
                }

                if (!string.IsNullOrEmpty(employeeNo))
                {
                    query = query.Where(k => k.EmployeeNo.ToLower().Contains(employeeNo.ToLower()));
                }

                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                }

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Approved")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Approved" && 
                            (k.ManagerStatus ?? "Pending") == "Approved"
                        );
                    }
                    else if (status == "Rejected")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") == "Rejected" || 
                            (k.ManagerStatus ?? "Pending") == "Rejected"
                        );
                    }
                    else if (status == "Pending")
                    {
                        query = query.Where(k => 
                            (k.EngineerStatus ?? "Pending") != "Rejected" && 
                            (k.ManagerStatus ?? "Pending") != "Rejected" &&
                            !((k.EngineerStatus ?? "Pending") == "Approved" && (k.ManagerStatus ?? "Pending") == "Approved")
                        );
                    }
                }

                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                }

                // Apply quarter filter if specified
                if (!string.IsNullOrEmpty(quarter) && int.TryParse(quarter, out int quarterNum))
                {
                    var quarterStart = new DateTime(DateTime.Now.Year, ((quarterNum - 1) * 3) + 1, 1);
                    var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                    
                    query = query.Where(k => k.DateSubmitted >= quarterStart && k.DateSubmitted <= quarterEnd);
                }
                // If no date filters and no quarter filter are applied, default to current quarter
                else if (string.IsNullOrEmpty(startDate) && string.IsNullOrEmpty(endDate))
                {
                    var currentQuarter = ((DateTime.Now.Month - 1) / 3) + 1;
                    var quarterStart = new DateTime(DateTime.Now.Year, ((currentQuarter - 1) * 3) + 1, 1);
                    var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                    
                    query = query.Where(k => k.DateSubmitted >= quarterStart && k.DateSubmitted <= quarterEnd);
                }

                if (!string.IsNullOrEmpty(costSavingRange))
                {
                    switch (costSavingRange)
                    {
                        case "0-1000":
                            query = query.Where(k => k.CostSaving >= 0 && k.CostSaving <= 1000);
                            break;
                        case "1001-5000":
                            query = query.Where(k => k.CostSaving > 1000 && k.CostSaving <= 5000);
                            break;
                        case "5001-10000":
                            query = query.Where(k => k.CostSaving > 5000 && k.CostSaving <= 10000);
                            break;
                        case "10001+":
                            query = query.Where(k => k.CostSaving > 10000);
                            break;
                        case "No Cost Saving":
                            query = query.Where(k => k.CostSaving == null || k.CostSaving == 0);
                            break;
                    }
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();

                // Create comprehensive CSV content with detailed information
                var csv = new StringBuilder();
                
                // Add comprehensive headers (without image columns)
                csv.AppendLine("KAIZEN NO,EMPLOYEE NO,EMPLOYEE NAME,DEPARTMENT,PLANT,DATE SUBMITTED,CATEGORY,SUGGESTION DESCRIPTION,COST SAVING PER YEAR ($),DOLLAR RATE,OTHER BENEFITS,ENGINEER STATUS,ENGINEER APPROVED BY,MANAGER STATUS,MANAGER APPROVED BY,OVERALL STATUS,EXECUTIVE COMMENTS,IMPLEMENTATION AREA,CAN IMPLEMENT IN OTHER FIELDS,MANAGER COMMENTS,MANAGER SIGNATURE,AWARD PRICE,AWARD CATEGORY 1ST,AWARD CATEGORY 2ND,AWARD CATEGORY 3RD,REJECTION REASON ENGINEER,REJECTION REASON MANAGER,QUARTER CHANGE");

                // Add data rows
                foreach (var kaizen in kaizens)
                {
                    var engStatus = kaizen.EngineerStatus ?? "Pending";
                    var mgrStatus = kaizen.ManagerStatus ?? "Pending";
                    
                    // Calculate overall status
                    var overallStatus = "Pending";
                    if (engStatus == "Rejected" || mgrStatus == "Rejected")
                    {
                        overallStatus = "Rejected";
                    }
                    else if (engStatus == "Approved" && mgrStatus == "Approved")
                    {
                        overallStatus = "Approved";
                    }

                    // Determine award categories
                    var award1st = "";
                    var award2nd = "";
                    var award3rd = "";
                    
                    // Award calculation is now dynamic based on scores
                    // This will be calculated separately if needed for export

                    // Get quarter information
                    var kaizenQuarter = ((kaizen.DateSubmitted.Month - 1) / 3) + 1;
                    var quarterText = kaizenQuarter switch
                    {
                        1 => "Q1",
                        2 => "Q2",
                        3 => "Q3",
                        4 => "Q4",
                        _ => "Q1"
                    };

                    // Escape CSV values and create comprehensive row
                    var row = new List<string>
                    {
                        EscapeCsvValue(kaizen.KaizenNo),
                        EscapeCsvValue(kaizen.EmployeeNo),
                        EscapeCsvValue(kaizen.EmployeeName),
                        EscapeCsvValue(kaizen.Department),
                        EscapeCsvValue(kaizen.Plant ?? "KTY"),
                        EscapeCsvValue(kaizen.DateSubmitted.ToString("yyyy-MM-dd")),
                        EscapeCsvValue(kaizen.Category ?? ""),
                        EscapeCsvValue(kaizen.SuggestionDescription ?? ""),
                        EscapeCsvValue(kaizen.CostSaving.HasValue ? kaizen.CostSaving.Value.ToString("N0") : ""),
                        EscapeCsvValue(kaizen.DollarRate.HasValue ? kaizen.DollarRate.Value.ToString("N2") : ""),
                        EscapeCsvValue(kaizen.OtherBenefits ?? ""),
                        EscapeCsvValue(engStatus),
                        EscapeCsvValue(kaizen.EngineerApprovedBy ?? ""),
                        EscapeCsvValue(mgrStatus),
                        EscapeCsvValue(kaizen.ManagerApprovedBy ?? ""),
                        EscapeCsvValue(overallStatus),
                        EscapeCsvValue(kaizen.Comments ?? ""),
                        EscapeCsvValue(kaizen.ImplementationArea ?? ""),
                        EscapeCsvValue(kaizen.CanImplementInOtherFields ?? ""),
                        EscapeCsvValue(kaizen.ManagerComments ?? ""),
                        EscapeCsvValue(kaizen.ManagerSignature ?? ""),
                        EscapeCsvValue(""), // Award price is now dynamic
                        EscapeCsvValue(award1st),
                        EscapeCsvValue(award2nd),
                        EscapeCsvValue(award3rd),
                        EscapeCsvValue(""), // Rejection Reason Engineer (not available in current model)
                        EscapeCsvValue(""), // Rejection Reason Manager (not available in current model)
                        EscapeCsvValue(quarterText)
                    };

                    csv.AppendLine(string.Join(",", row));
                }

                // Use only the data table without summary
                var finalCsv = csv.ToString();

                // Generate descriptive filename
                var fileNameQuarter = ((DateTime.Now.Month - 1) / 3) + 1;
                if (!string.IsNullOrEmpty(quarter) && int.TryParse(quarter, out int fileQuarter))
                {
                    fileNameQuarter = fileQuarter;
                }
                
                var fileNameQuarterText = fileNameQuarter switch
                {
                    1 => "Q1",
                    2 => "Q2", 
                    3 => "Q3",
                    4 => "Q4",
                    _ => "Q1"
                };

                var filterDescription = new List<string>();
                if (!string.IsNullOrEmpty(department)) filterDescription.Add(department);
                if (!string.IsNullOrEmpty(status)) filterDescription.Add(status);
                if (!string.IsNullOrEmpty(quarter)) filterDescription.Add($"Q{quarter}");
                
                var filterSuffix = filterDescription.Any() ? $"_{string.Join("_", filterDescription)}" : "";
                var fileName = $"KAIZEN_Detailed_Export_{fileNameQuarterText}_{DateTime.Now.Year}{filterSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                // Return the CSV file
                var content = Encoding.UTF8.GetBytes(finalCsv);
                return File(content, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExportToExcel: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // Supervisor User Management Methods
        [HttpGet]
        public IActionResult SupervisorUserManagement()
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            if (!IsSupervisorRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Get only users with "User" role
            var users = _context.Users.Where(u => u.Role == "User").ToList();
            return View("~/Views/Kaizen/SupervisorUserManagement.cshtml", users);
        }

        [HttpGet]
        public IActionResult GetSupervisorUserDetails(int id)
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            if (!IsSupervisorRole())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var role = user.Role ?? "User";
            var roleDisplay = role switch
            {
                "Admin" => "Administrator",
                "KaizenTeam" => "Kaizen Team",
                _ => role
            };

            var userDetails = new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    departmentName = user.DepartmentName,
                    employeeName = user.EmployeeName ?? "",
                    employeeNumber = user.EmployeeNumber ?? "",
                    role = roleDisplay,
                    plant = user.Plant ?? "",
                    employeePhotoPath = user.EmployeePhotoPath ?? ""
                }
            };

            return Json(userDetails);
        }

        [HttpGet]
        public IActionResult EditSupervisorUser(int id)
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            if (!IsSupervisorRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View("~/Views/Kaizen/EditSupervisorUser.cshtml", user);
        }

        [HttpPost]
        public IActionResult EditSupervisorUser(Users model)
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            if (!IsSupervisorRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Remove ConfirmPassword validation for editing
            ModelState.Remove("ConfirmPassword");

            if (!ModelState.IsValid)
            {
                return View("~/Views/Kaizen/EditSupervisorUser.cshtml", model);
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == model.Id);
            if (user == null)
            {
                return NotFound();
            }

            // Check if username already exists for another user
            var existingUser = _context.Users.FirstOrDefault(u => u.UserName == model.UserName && u.Id != model.Id);
            if (existingUser != null)
            {
                ModelState.AddModelError("UserName", "Username already exists.");
                return View("~/Views/Kaizen/EditSupervisorUser.cshtml", model);
            }

            // Validate password confirmation if password is provided
            if (!string.IsNullOrEmpty(model.Password) && model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return View("~/Views/Kaizen/EditSupervisorUser.cshtml", model);
            }

            user.UserName = model.UserName;
            user.DepartmentName = model.DepartmentName;
            user.EmployeeName = model.EmployeeName;
            user.EmployeeNumber = model.EmployeeNumber;
            user.Plant = model.Plant;
            
            // Only update password if a new one is provided
            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = model.Password;
            }

            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "User updated successfully!";
            return RedirectToAction("SupervisorUserManagement");
        }

        [HttpPost]
        public IActionResult DeleteSupervisorUser(int id)
        {
            // Check if user is supervisor
            var username = User.Identity?.Name;
            if (!IsSupervisorRole())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Prevent deletion of admin user
            if (user.UserName?.ToLower() == "admin")
            {
                return Json(new { success = false, message = "Cannot delete the admin user." });
            }

            try
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                return Json(new { success = true, message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting user: " + ex.Message });
            }
        }

        // GET: /Kaizen/TestEmail - Test endpoint to verify email functionality
        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers and engineers to test email functionality
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers and engineers can test email functionality." });
            }

            try
            {
                Console.WriteLine("=== TESTING EMAIL FUNCTIONALITY ===");
                
                // Get current user's department
                var userDepartment = await GetCurrentUserDepartment();
                Console.WriteLine($"Current user department: {userDepartment}");

                if (string.IsNullOrEmpty(userDepartment))
                {
                    return Json(new { success = false, message = "No department found for current user." });
                }

                // Find engineer in the same department
                var engineer = await _context.Users
                    .Where(u => u.DepartmentName == userDepartment && 
                               u.Role.ToLower() == "engineer" && 
                               !string.IsNullOrEmpty(u.Email))
                    .FirstOrDefaultAsync();

                if (engineer == null)
                {
                    return Json(new { success = false, message = $"No engineer found in department: {userDepartment}" });
                }

                Console.WriteLine($"Found engineer: {engineer.EmployeeName} ({engineer.Email})");

                // Generate website URL
                var websiteUrl = $"{Request.Scheme}://{Request.Host}";
                Console.WriteLine($"Website URL: {websiteUrl}");

                // Send test email with similar suggestions
                var similarKaizens = await _kaizenService.GetSimilarKaizensAsync(
                    "This is a test kaizen suggestion to verify email functionality.",
                    "HasCostSaving",
                    "Test benefits for email functionality verification.",
                    userDepartment,
                    0 // Use 0 as current kaizen ID for test
                );

                var emailSent = await _emailService.SendKaizenNotificationWithSimilarSuggestionsAsync(
                    engineer.Email ?? "",
                    "TEST-KAIZEN-001",
                    "Test Employee",
                    userDepartment,
                    "This is a test kaizen suggestion to verify email functionality.",
                    websiteUrl,
                    similarKaizens
                );

                if (emailSent)
                {
                    Console.WriteLine($"Test email sent successfully to {engineer.Email}");
                    return Json(new { 
                        success = true, 
                        message = $"Test email sent successfully to {engineer.Email}",
                        engineerEmail = engineer.Email,
                        department = userDepartment
                    });
                }
                else
                {
                    Console.WriteLine($"Failed to send test email to {engineer.Email}");
                    return Json(new { 
                        success = false, 
                        message = $"Failed to send test email to {engineer.Email}",
                        engineerEmail = engineer.Email,
                        department = userDepartment
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestEmail: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> InterDeptApproval([FromBody] InterDeptApprovalRequest request)
        {
            try
            {
                // Check if user is engineer
                if (!IsEngineerRole())
                {
                    return Json(new { success = false, message = "Access denied. Only engineers can perform inter-department approvals." });
                }

                // Get current user
                var username = User.Identity?.Name;
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Get the kaizen form
                var kaizen = await _context.KaizenForms.FirstOrDefaultAsync(k => k.Id == request.KaizenId);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                // Check if the current user's department is in the implementation area
                var implementationAreas = kaizen.ImplementationArea?.Split(',').Select(d => d.Trim()).ToList() ?? new List<string>();
                if (!implementationAreas.Contains(currentUser.DepartmentName))
                {
                    return Json(new { success = false, message = "You can only approve/reject kaizens for your own department." });
                }

                // Check if the requested department is in the implementation area
                if (!implementationAreas.Contains(request.Department))
                {
                    return Json(new { success = false, message = "The selected department is not in the implementation area." });
                }

                // Initialize approval tracking if not exists
                if (string.IsNullOrEmpty(kaizen.InterDeptApprovedDepartments))
                {
                    kaizen.InterDeptApprovedDepartments = "";
                }
                if (string.IsNullOrEmpty(kaizen.InterDeptRejectedDepartments))
                {
                    kaizen.InterDeptRejectedDepartments = "";
                }

                var approvedDepartments = kaizen.InterDeptApprovedDepartments.Split(',').Where(d => !string.IsNullOrEmpty(d.Trim())).ToList();
                var rejectedDepartments = kaizen.InterDeptRejectedDepartments.Split(',').Where(d => !string.IsNullOrEmpty(d.Trim())).ToList();

                if (request.Action.ToLower() == "approve")
                {
                    // Add to approved departments if not already there
                    if (!approvedDepartments.Contains(request.Department))
                    {
                        approvedDepartments.Add(request.Department);
                    }
                    // Remove from rejected departments if it was there
                    rejectedDepartments.Remove(request.Department);
                }
                else if (request.Action.ToLower() == "reject")
                {
                    // Add to rejected departments if not already there
                    if (!rejectedDepartments.Contains(request.Department))
                    {
                        rejectedDepartments.Add(request.Department);
                    }
                    // Remove from approved departments if it was there
                    approvedDepartments.Remove(request.Department);

                    // Remove the department from implementation area
                    implementationAreas.Remove(request.Department);
                    kaizen.ImplementationArea = string.Join(",", implementationAreas);
                }

                // Update the approval tracking fields
                kaizen.InterDeptApprovedDepartments = string.Join(",", approvedDepartments);
                kaizen.InterDeptRejectedDepartments = string.Join(",", rejectedDepartments);
                kaizen.InterDeptApprovedBy = currentUser.EmployeeName;
                kaizen.InterDeptStatus = "Processed";

                await _context.SaveChangesAsync();

                string actionText = request.Action.ToLower() == "approve" ? "approved" : "rejected";
                string message = $"Department '{request.Department}' has been {actionText} successfully.";
                
                if (request.Action.ToLower() == "reject")
                {
                    message += " The department has been removed from the implementation area.";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InterDeptApproval: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Award Tracking action for Kaizen Team
        public IActionResult AwardTracking(string startDate, string endDate, string department, string category, string awardStatus)
        {
            // Check if user is kaizen team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Get base query for approved kaizens
            var query = _context.KaizenForms
                .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

            // Apply date range filter
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
            {
                query = query.Where(k => k.DateSubmitted >= start);
            }

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
            {
                // Add one day to include the end date
                end = end.AddDays(1);
                query = query.Where(k => k.DateSubmitted < end);
            }

            // Apply department filter
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(k => k.Department == department);
            }

            // Apply category filter
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(k => k.Category != null && k.Category.Contains(category));
            }

            // Apply award status filter
            if (!string.IsNullOrEmpty(awardStatus))
            {
                if (awardStatus == "Pending")
                {
                    // Filter for kaizens that don't have scores yet (will be filtered after calculation)
                }
                else if (awardStatus == "Assigned")
                {
                    // Filter for kaizens that have scores (will be filtered after calculation)
                }
            }

            // Get filtered results
            var approvedKaizens = query.ToList();

            // Calculate scores for each kaizen
            var kaizensWithScores = new List<object>();
            foreach (var kaizen in approvedKaizens)
            {
                var scores = _context.KaizenMarkingScores.Where(s => s.KaizenId == kaizen.Id).ToList();
                
                // Get the total weight of criteria that were actually scored for this kaizen
                var scoredCriteriaIds = scores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = _context.MarkingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                
                var totalScore = scores.Sum(s => s.Score);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;

                kaizensWithScores.Add(new
                {
                    Kaizen = kaizen,
                    Score = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage
                });
            }

            // Sort by percentage in descending order (highest score first)
            kaizensWithScores = kaizensWithScores
                .OrderByDescending(k => ((dynamic)k).Percentage)
                .ThenByDescending(k => ((dynamic)k).Score)
                .ThenByDescending(k => ((dynamic)k).Kaizen.DateSubmitted)
                .ToList();

            // Populate ViewBag with filter options
            ViewBag.Departments = _context.KaizenForms
                .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && !string.IsNullOrEmpty(k.Department))
                .Select(k => k.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Get all unique categories from approved kaizens
            var allCategories = new List<string>();
            var kaizensWithCategories = _context.KaizenForms
                .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && !string.IsNullOrEmpty(k.Category))
                .Select(k => k.Category)
                .ToList();

            foreach (var catString in kaizensWithCategories)
            {
                if (!string.IsNullOrEmpty(catString))
                {
                    var categories = catString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c));
                    allCategories.AddRange(categories);
                }
            }

            ViewBag.Categories = allCategories.Distinct().OrderBy(c => c).ToList();

            // Add award information to each kaizen
            var kaizensWithAwards = new List<object>();
            foreach (dynamic item in kaizensWithScores)
            {
                var awardInfo = GetAwardForPercentage(item.Percentage);
                kaizensWithAwards.Add(new
                {
                    Kaizen = item.Kaizen,
                    Score = item.Score,
                    TotalWeight = item.TotalWeight,
                    Percentage = item.Percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                });
            }

            return View(kaizensWithAwards);
        }

        // GET: /Kaizen/ExportAwardTrackingToExcel
        [HttpGet]
        public IActionResult ExportAwardTrackingToExcel(string startDate, string endDate, string department, string awardStatus)
        {
            // Check if user is kaizen team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Get base query for approved kaizens (same logic as AwardTracking action)
                var query = _context.KaizenForms
                    .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved");

                // Apply date range filter
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
                {
                    // Add one day to include the end date
                    end = end.AddDays(1);
                    query = query.Where(k => k.DateSubmitted < end);
                }

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                }


                // Note: Award status filtering will be applied after dynamic calculation

                // Get filtered results
                var approvedKaizens = query.ToList();

                // Create CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Kaizen No,Employee No,Employee Name,Department,Date Submitted,Cost Saving,Award Status,Award Price");

                foreach (var kaizen in approvedKaizens)
                {
                    // Award status is now dynamic based on scores
                    var hasScores = _context.KaizenMarkingScores.Any(s => s.KaizenId == kaizen.Id);
                    var currentAwardStatus = hasScores ? "Awarded" : "Pending";

                    // Escape CSV values and create row
                    var row = new List<string>
                    {
                        EscapeCsvValue(kaizen.KaizenNo),
                        EscapeCsvValue(kaizen.EmployeeNo),
                        EscapeCsvValue(kaizen.EmployeeName),
                        EscapeCsvValue(kaizen.Department),
                        EscapeCsvValue(kaizen.DateSubmitted.ToString("yyyy-MM-dd")),
                        EscapeCsvValue(kaizen.CostSaving.HasValue ? kaizen.CostSaving.Value.ToString("N2") : ""),
                        EscapeCsvValue(currentAwardStatus),
                        EscapeCsvValue("") // Award price is now dynamic
                    };

                    csv.AppendLine(string.Join(",", row));
                }

                // Return CSV file
                var fileName = $"Award_Tracking_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExportAwardTrackingToExcel: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/TeamMarksDetails - Team marks details for Kaizen Team (Read-Only)
        [HttpGet]
        public async Task<IActionResult> TeamMarksDetails(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow Kaizen Team
            if (!IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == id);
                if (kaizen == null)
                {
                    return NotFound();
                }

                // Get active marking criteria
                var markingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
                
                // Get existing scores for this kaizen
                var existingScores = await _context.KaizenMarkingScores
                    .Where(s => s.KaizenId == id)
                    .ToListAsync();
                
                // Calculate total score and percentage
                var totalScore = existingScores.Sum(s => s.Score);
                var scoredCriteriaIds = existingScores.Select(s => s.MarkingCriteriaId).ToList();
                var totalWeight = markingCriteria
                    .Where(c => scoredCriteriaIds.Contains(c.Id))
                    .Sum(c => c.Weight);
                var percentage = totalWeight > 0 ? Math.Round((double)totalScore / totalWeight * 100, 1) : 0;
                
                // Get award information based on percentage
                var awardInfo = GetAwardForPercentage(percentage);
                
                // Create a view model to pass both kaizen and marking criteria
                var viewModel = new AwardDetailsViewModel
                {
                    Kaizen = kaizen,
                    MarkingCriteria = markingCriteria,
                    ExistingScores = existingScores,
                    TotalScore = totalScore,
                    TotalWeight = totalWeight,
                    Percentage = percentage,
                    AwardName = awardInfo.Item1,
                    AwardClass = awardInfo.Item2
                };

                return View("TeamMarksDetails", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TeamMarksDetails: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // Helper method to determine award based on percentage
        private (string AwardName, string AwardClass) GetAwardForPercentage(double percentage)
        {
            try
            {
                // Get all active award thresholds ordered by minimum percentage (highest first)
                var awardThresholds = _context.AwardThresholds
                    .Where(t => t.IsActive)
                    .OrderByDescending(t => t.MinPercentage)
                    .ToList();

                // Find the appropriate award range
                foreach (var threshold in awardThresholds)
                {
                    if (percentage >= (double)threshold.MinPercentage && percentage <= (double)threshold.MaxPercentage)
                    {
                        // Determine CSS class based on award name
                        string awardClass = threshold.AwardName.ToLower().Contains("1st") || threshold.AwardName.ToLower().Contains("first") ? "bg-success" :
                                          threshold.AwardName.ToLower().Contains("2nd") || threshold.AwardName.ToLower().Contains("second") ? "bg-warning" :
                                          threshold.AwardName.ToLower().Contains("3rd") || threshold.AwardName.ToLower().Contains("third") ? "bg-info" :
                                          "bg-primary";

                        return (threshold.AwardName, awardClass);
                    }
                }

                // If no award range matches, return "No Award"
                return ("No Award", "bg-secondary");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting award for percentage {percentage}: {ex.Message}");
                return ("No Award", "bg-secondary");
            }
        }
    }

    public class InterDeptApprovalRequest
    {
        public int KaizenId { get; set; }
        public string Department { get; set; }
        public string Action { get; set; } // "approve" or "reject"
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; }
        public string RejectorName { get; set; }
        public string ApproverName { get; set; }
        public string ApproverType { get; set; } // "Engineer" or "Manager"
    }

    public class UpdateEngineerStatusRequest
    {
        public string EngineerStatus { get; set; }
        public string EngineerApprovedBy { get; set; }
    }

    public class UpdateManagerStatusRequest
    {
        public string ManagerStatus { get; set; }
        public string ManagerApprovedBy { get; set; }
    }



    public class ManagerCommentRequest
    {
        public string Comments { get; set; }
        public string Signature { get; set; }
    }
}