using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace KaizenWebApp.Controllers
{
    [Authorize]
    public class KaizenController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public KaizenController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
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
            var username = User?.Identity?.Name;
            return username?.ToLower().Contains("kaizenteam") == true;
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
                var viewModel = new KaizenFormViewModel
                {
                    KaizenNo = GenerateKaizenNo(),
                    DateSubmitted = DateTime.Today,
                    CostSavingType = "NoCostSaving" // Set default value
                };

                // Debug information
                Console.WriteLine($"User authenticated: {User.Identity?.IsAuthenticated}");
                Console.WriteLine($"User name: {User.Identity?.Name}");
                Console.WriteLine($"User claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}"))}");

                // Auto-populate department based on logged-in user
                try
                {
                    var department = await GetCurrentUserDepartment();
                    viewModel.Department = department;
                    Console.WriteLine($"Setting department in viewModel: {department}");
                    
                    // Additional debug info
                    if (department != null)
                    {
                        Console.WriteLine($"Department successfully set to: '{department}'");
                    }
                    else
                    {
                        Console.WriteLine("Department is null - user not found or no department");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting user department: {ex.Message}");
                    viewModel.Department = null; // Set to null if there's an error
                }

                return View("~/Views/Home/Kaizenform.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Kaizenform action: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Return a basic view model if there's an error
                var fallbackViewModel = new KaizenFormViewModel
                {
                    KaizenNo = GenerateKaizenNo(),
                    DateSubmitted = DateTime.Today,
                    Department = null
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer");
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
                if (viewModel.BeforeKaizenImage != null && !IsValidImage(viewModel.BeforeKaizenImage))
                {
                    ModelState.AddModelError("BeforeKaizenImage", "Invalid image format. Only PNG, JPG, JPEG, WebP files up to 5MB are allowed.");
                    errorStep = 3; // Step 3 contains BeforeKaizenImage
                    hasImageError = true;
                }

                if (viewModel.AfterKaizenImage != null && !IsValidImage(viewModel.AfterKaizenImage))
                {
                    ModelState.AddModelError("AfterKaizenImage", "Invalid image format. Only PNG, JPG, JPEG, WebP files up to 5MB are allowed.");
                    errorStep = 3; // Step 3 contains AfterKaizenImage
                    hasImageError = true;
                }

                if (viewModel.EmployeePhoto != null && !IsValidImage(viewModel.EmployeePhoto))
                {
                    ModelState.AddModelError("EmployeePhoto", "Invalid image format. Only PNG, JPG, JPEG, WebP files up to 5MB are allowed.");
                    errorStep = 2; // Step 2 contains EmployeePhoto
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
                    KaizenNo = viewModel.KaizenNo ?? GenerateKaizenNo(),
                    DateSubmitted = viewModel.DateSubmitted,
                    DateImplemented = viewModel.DateImplemented,
                    Department = finalDepartment,
                    EmployeeName = viewModel.EmployeeName?.Trim(),
                    EmployeeNo = viewModel.EmployeeNo?.Trim(),
                    SuggestionDescription = viewModel.SuggestionDescription?.Trim(),

                    CostSaving = viewModel.CostSaving,
                    CostSavingType = viewModel.CostSavingType,
                    DollarRate = viewModel.DollarRate,
                    OtherBenefits = viewModel.OtherBenefits?.Trim()
                };

                // Handle file uploads for BeforeKaizenImage
                if (viewModel.BeforeKaizenImage != null && viewModel.BeforeKaizenImage.Length > 0)
                {
                    string beforeFileName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.BeforeKaizenImage.FileName)}";
                    string beforePath = Path.Combine("uploads", beforeFileName);
                    string fullBeforePath = Path.Combine(_env.WebRootPath, beforePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullBeforePath));
                    using (var stream = new FileStream(fullBeforePath, FileMode.Create))
                    {
                        await viewModel.BeforeKaizenImage.CopyToAsync(stream);
                    }
                    model.BeforeKaizenImagePath = "/" + beforePath.Replace("\\", "/");
                }

                // Handle file uploads for AfterKaizenImage
                if (viewModel.AfterKaizenImage != null && viewModel.AfterKaizenImage.Length > 0)
                {
                    string afterFileName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.AfterKaizenImage.FileName)}";
                    string afterPath = Path.Combine("uploads", afterFileName);
                    string fullAfterPath = Path.Combine(_env.WebRootPath, afterPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullAfterPath));
                    using (var stream = new FileStream(fullAfterPath, FileMode.Create))
                    {
                        await viewModel.AfterKaizenImage.CopyToAsync(stream);
                    }
                    model.AfterKaizenImagePath = "/" + afterPath.Replace("\\", "/");
                }

                // Handle file uploads for EmployeePhoto
                if (viewModel.EmployeePhoto != null && viewModel.EmployeePhoto.Length > 0)
                {
                    string employeePhotoFileName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.EmployeePhoto.FileName)}";
                    string employeePhotoPath = Path.Combine("uploads", employeePhotoFileName);
                    string fullEmployeePhotoPath = Path.Combine(_env.WebRootPath, employeePhotoPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullEmployeePhotoPath));
                    using (var stream = new FileStream(fullEmployeePhotoPath, FileMode.Create))
                    {
                        await viewModel.EmployeePhoto.CopyToAsync(stream);
                    }
                    model.EmployeePhotoPath = "/" + employeePhotoPath.Replace("\\", "/");
                }

                Console.WriteLine("About to save to database...");
                _context.KaizenForms.Add(model);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Kaizen saved successfully with ID: {model.Id}");
                Console.WriteLine($"KaizenNo: {model.KaizenNo}");
                Console.WriteLine($"EmployeeName: {model.EmployeeName}");

                TempData["Success"] = "Kaizen suggestion submitted successfully!";
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

        private string GenerateKaizenNo()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            int count = _context.KaizenForms.Count(k => k.DateSubmitted.Date == DateTime.Today) + 1;
            return $"KZN-{datePart}-{count:D3}";
        }

        private bool IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > 5 * 1024 * 1024) return false; // Max 5MB
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
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

        // ------------------- MANAGER FUNCTIONALITY -------------------

        [HttpGet]
        public async Task<IActionResult> KaizenListEngineer(string searchString, string startDate, string endDate, string category, string engineerStatus)
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
                Console.WriteLine($"SearchString: {searchString}, StartDate: {startDate}, EndDate: {endDate}, Category: {category}, EngineerStatus: {engineerStatus}");

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

                // Filter by engineer status
                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    query = query.Where(k => (k.EngineerStatus ?? "Pending") == engineerStatus);
                    Console.WriteLine($"Applied engineer status filter: {engineerStatus}");
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
        public async Task<IActionResult> KaizenListManager(string searchString, string status, string category, string managerStatus, string startDate, string endDate)
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer"); // Default fallback
                }
            }

            try
            {
                Console.WriteLine($"=== KAIZENLISTMANAGER DEBUG ===");
                Console.WriteLine($"KaizenListManager called by: {username}");
                Console.WriteLine($"SearchString: {searchString}, Status: {status}, Category: {category}, ManagerStatus: {managerStatus}, StartDate: {startDate}, EndDate: {endDate}");

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

                // Filter to show only kaizens where engineer has approved (for manager review)
                query = query.Where(k => k.EngineerStatus == "Approved");
                Console.WriteLine("Filtered to show only kaizens where engineer has approved (for manager review)");
                
                // Debug: Check count after EngineerStatus filter
                var afterEngineerStatusFilter = await query.CountAsync();
                Console.WriteLine($"Kaizens after EngineerStatus filter: {afterEngineerStatusFilter}");

                // Filter to show only kaizens with executive filling data
                query = query.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                );
                Console.WriteLine("Filtered to show only kaizens with completed executive filling");
                
                // Debug: Check count after executive filling filter
                var afterExecutiveFillingFilter = await query.CountAsync();
                Console.WriteLine($"Kaizens after executive filling filter: {afterExecutiveFillingFilter}");

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

                // Filter by manager status
                if (!string.IsNullOrEmpty(managerStatus))
                {
                    query = query.Where(k => (k.ManagerStatus ?? "Pending") == managerStatus);
                    Console.WriteLine($"Applied manager status filter: {managerStatus}");
                }
                else if (!string.IsNullOrEmpty(status))
                {
                    // Fallback to the old 'status' parameter for backward compatibility
                    query = query.Where(k => (k.ManagerStatus ?? "Pending") == status);
                    Console.WriteLine($"Applied manager status filter (legacy): {status}");
                }

                // Filter by date range
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(k => k.DateSubmitted >= start);
                    Console.WriteLine($"Applied start date filter: {startDate}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    query = query.Where(k => k.DateSubmitted <= end);
                    Console.WriteLine($"Applied end date filter: {endDate}");
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
        public async Task<IActionResult> UserKaizenList(string searchString, string department)
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer");
                }
            }

            try
            {
                var username = User?.Identity?.Name;
                var userDepartment = await GetCurrentUserDepartment();
                
                Console.WriteLine($"=== USERKAIZENLIST DEBUG ===");
                Console.WriteLine($"UserKaizenList called by: {username}, UserDepartment: {userDepartment}");
                Console.WriteLine($"SearchString: {searchString}, Department: {department}");

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

                // Get all unique departments for filter
                ViewBag.Departments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .OrderBy(d => d)
                    .Distinct()
                    .ToListAsync();

                return View("~/Views/Home/UserKaizenList.cshtml", kaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserKaizenList: {ex.Message}");
                return View("~/Views/Home/UserKaizenList.cshtml", new List<KaizenForm>());
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
            catch (Exception ex)
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
            catch (Exception ex)
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
            var username = User.Identity?.Name;
            if (username?.ToLower().Contains("kaizenteam") != true)
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

            // Awarded kaizens: where award price is assigned
            var awardedKaizens = _context.KaizenForms.Count(k => 
                !string.IsNullOrEmpty(k.AwardPrice));

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
            string startDate, string endDate, string category, string engineerStatus, string managerStatus, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo)
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer"); // Default fallback
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

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
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
                    KaizenNo = kaizenNo
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
            string startDate, string endDate, string category, string engineerStatus, string managerStatus, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo)
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

                // Apply category filter
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(k => k.Category != null && k.Category.Contains(category));
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

        // GET: /Kaizen/DepartmentTargets - Read-only department targets for kaizen team
        [HttpGet]
        public async Task<IActionResult> DepartmentTargets(int? year, int? month, string statusFilter)
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("KaizenListEngineer"); // Default fallback
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

                // Filter by status if provided
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    if (statusFilter == "At Risk")
                    {
                        viewModel.DepartmentTargets = viewModel.DepartmentTargets
                            .Where(dt => dt.Status == "At Risk")
                            .ToList();
                    }
                    else if (statusFilter == "Safe")
                    {
                        viewModel.DepartmentTargets = viewModel.DepartmentTargets
                            .Where(dt => dt.Status != "At Risk")
                            .ToList();
                    }
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

                return View("~/Views/Kaizen/KaizenDetails.cshtml", kaizen);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the kaizen details.";
                return RedirectToAction("KaizenListManager");
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

                // Calculate cost savings (only from approved kaizens)
                var currentMonthCostSavings = currentMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved")
                    .Sum(k => k.CostSaving ?? 0);
                var previousMonthCostSavings = previousMonthKaizens
                    .Where(k => k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved")
                    .Sum(k => k.CostSaving ?? 0);

                // Calculate pending approvals
                var pendingApprovals = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Pending" || k.ManagerStatus == null);

                // Calculate completed kaizens
                var completedKaizens = currentMonthKaizens.Count(k => 
                    k.ManagerStatus == "Approved" && k.EngineerStatus == "Approved");

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

        // GET: /Kaizen/AwardTracking - Read-only award tracking for kaizen team
        [HttpGet]
        public async Task<IActionResult> AwardTracking(string startDate, string endDate, string department, string category, string awardStatus)
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
                    return RedirectToAction("KaizenListEngineer");
                }
                else
                {
                    return RedirectToAction("AccessDenied", "Home");
                }
            }

            try
            {
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
                        // Filter for kaizens that don't have an award price assigned
                        query = query.Where(k => string.IsNullOrEmpty(k.AwardPrice));
                    }
                    else
                    {
                        // Filter for kaizens with the specific award price
                        query = query.Where(k => k.AwardPrice == awardStatus);
                    }
                }

                // Get filtered results
                var approvedKaizens = query.OrderByDescending(k => k.DateSubmitted).ToList();

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

                return View("AwardTrackingView", approvedKaizens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AwardTracking: {ex.Message}");
                return RedirectToAction("Error", "Home");
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

                // Apply award status filter
                if (!string.IsNullOrEmpty(awardStatus))
                {
                    if (awardStatus == "Pending")
                    {
                        // Filter for kaizens that don't have an award price assigned
                        query = query.Where(k => string.IsNullOrEmpty(k.AwardPrice));
                    }
                    else
                    {
                        // Filter for kaizens with the specific award price
                        query = query.Where(k => k.AwardPrice == awardStatus);
                    }
                }

                // Get filtered results
                var approvedKaizens = query.OrderByDescending(k => k.DateSubmitted).ToList();

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

                return View("AwardTrackingManager", approvedKaizens);
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

                return View("AwardDetailsManager", kaizen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AwardDetailsManager: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Kaizen/AwardDetails - Award details for Kaizen Team
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

                return View("AwardDetails", kaizen);
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

                kaizen.AwardPrice = awardPrice;
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

                kaizen.AwardPrice = awardPrice;
                kaizen.CommitteeComments = committeeComments;
                kaizen.CommitteeSignature = committeeSignature;
                kaizen.AwardDate = DateTime.Now;

                _context.SaveChanges();

                TempData["SuccessMessage"] = "Award assigned successfully!";
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
            var kaizen = await _context.KaizenForms
                .FirstOrDefaultAsync(k => k.Id == id);

            if (kaizen == null)
            {
                return NotFound();
            }

            return View(kaizen);
        }
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