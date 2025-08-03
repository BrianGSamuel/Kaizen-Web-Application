using KaizenWebApp.Data;
using KaizenWebApp.Models;
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
                
                // Validate file uploads
                if (viewModel.BeforeKaizenImage != null && !IsValidImage(viewModel.BeforeKaizenImage))
                {
                    ModelState.AddModelError("BeforeKaizenImage", "Invalid image file. Only PNG, JPG, JPEG, WebP up to 5MB allowed.");
                    return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                }

                if (viewModel.AfterKaizenImage != null && !IsValidImage(viewModel.AfterKaizenImage))
                {
                    ModelState.AddModelError("AfterKaizenImage", "Invalid image file. Only PNG, JPG, JPEG, WebP up to 5MB allowed.");
                    return View("~/Views/Home/Kaizenform.cshtml", viewModel);
                }

                if (viewModel.EmployeePhoto != null && !IsValidImage(viewModel.EmployeePhoto))
                {
                    ModelState.AddModelError("EmployeePhoto", "Invalid image file. Only PNG, JPG, JPEG, WebP up to 5MB allowed.");
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
        public async Task<IActionResult> Search(string searchString, string department, string status)
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
                Console.WriteLine($"SearchString: '{searchString}', Department: '{department}', Status: '{status}'");
                
                // Debug: Check total kaizens in database
                var totalKaizens = await _context.KaizenForms.CountAsync();
                Console.WriteLine($"Total kaizens in database: {totalKaizens}");

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

                // For managers, filter to show only approved status kaizens
                if (isManager)
                {
                    // Debug: Check before status filter
                    var beforeStatusFilter = await query.CountAsync();
                    Console.WriteLine($"Search - Kaizens before status filter: {beforeStatusFilter}");
                    
                    query = query.Where(k => k.Status == "Approved");
                    Console.WriteLine("Filtered to show only approved status kaizens for manager");
                    
                    // Debug: Check after status filter
                    var afterStatusFilter = await query.CountAsync();
                    Console.WriteLine($"Search - Kaizens after status filter: {afterStatusFilter}");
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

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(k => k.Status == status);
                    Console.WriteLine($"Applied status filter: {status}");
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
                        status = k.Status,
                        approvedBy = k.ApprovedBy,
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

                Console.WriteLine($"Search returned {kaizens.Count} results");
                
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
                Console.WriteLine($"Status: {kaizen.Status}");
                Console.WriteLine($"Category: {kaizen.Category}");
                Console.WriteLine($"ApprovedBy: {kaizen.ApprovedBy}");
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
                        kaizen.Status,
                        kaizen.CostSaving,
                        kaizen.CostSavingType,
                        kaizen.DollarRate,
                        kaizen.OtherBenefits,
                        kaizen.BeforeKaizenImagePath,
                        kaizen.AfterKaizenImagePath,
                        kaizen.Category,
                        kaizen.ApprovedBy,
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
                    .Select(k => new { k.Id, k.KaizenNo, k.EmployeeName, k.Department, k.Status })
                    .ToListAsync();
                
                Console.WriteLine($"Found {results.Count} results");
                foreach (var r in results.Take(5))
                {
                    Console.WriteLine($"  - {r.KaizenNo}: {r.EmployeeName} ({r.Department}) - {r.Status}");
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
                    kaizen.Status,
                    kaizen.CostSaving,
                    kaizen.CostSavingType,
                    kaizen.DollarRate,
                    kaizen.OtherBenefits,
                    kaizen.BeforeKaizenImagePath,
                    kaizen.AfterKaizenImagePath
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

        // GET: /Kaizen/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users without "user" in their username)
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
                    Status = kaizen.Status,
                    CostSaving = kaizen.CostSaving,
                    CostSavingType = kaizen.CostSavingType,
                    DollarRate = kaizen.DollarRate,
                    OtherBenefits = kaizen.OtherBenefits,
                    BeforeKaizenImagePath = kaizen.BeforeKaizenImagePath,
                    AfterKaizenImagePath = kaizen.AfterKaizenImagePath,
                    EmployeePhotoPath = kaizen.EmployeePhotoPath,
                    // New fields
                    Category = kaizen.Category,
                    ApprovedBy = kaizen.ApprovedBy,
                    Comments = kaizen.Comments,
                    CanImplementInOtherFields = kaizen.CanImplementInOtherFields,
                    ImplementationArea = kaizen.ImplementationArea,
                    // Manager comment fields
                    ManagerComments = kaizen.ManagerComments,
                    ManagerSignature = kaizen.ManagerSignature
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

            // Only allow managers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can edit kaizens." });
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
                kaizen.ApprovedBy = viewModel.ApprovedBy?.Trim();
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
        public async Task<IActionResult> KaizenListEngineer(string searchString, string status)
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
                    return View("~/Views/Kaizen/KaizenListEngineer.cshtml", new List<KaizenForm>());
                }

                // Filter to show only Kaizen suggestions where executive filling fields are completed
                // This ensures engineers can see the executive filling data that belongs to each specific kaizen
                query = query.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.ApprovedBy) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                );
                Console.WriteLine("Filtered to show only Kaizen suggestions with completed executive filling");

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
                    query = query.Where(k => k.Status == status);
                    Console.WriteLine($"Applied status filter: {status}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListEngineer returned {kaizens.Count} results with executive filling data");
                
                // Debug: Show sample results with executive filling data
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    ApprovedBy: '{k.ApprovedBy}'");
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
        public async Task<IActionResult> KaizenListManager(string searchString, string status)
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
                Console.WriteLine($"=== KAIZENLISTMANAGER DEBUG ===");
                Console.WriteLine($"KaizenListManager called by: {username}");
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

                // Debug: Check query before filters
                var beforeFilters = await query.CountAsync();
                Console.WriteLine($"Kaizens before filters: {beforeFilters}");

                // Filter to show only approved status kaizens for executive
                query = query.Where(k => k.Status == "Approved");
                Console.WriteLine("Filtered to show only approved status kaizens");

                // Filter to show only kaizens with executive filling data
                query = query.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.ApprovedBy) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                );
                Console.WriteLine("Filtered to show only kaizens with completed executive filling");

                // Debug: Check query after status filter
                var afterStatusFilter = await query.CountAsync();
                Console.WriteLine($"Kaizens after status filter: {afterStatusFilter}");

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
                    query = query.Where(k => k.Status == status);
                    Console.WriteLine($"Applied status filter: {status}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListManager returned {kaizens.Count} results (only approved status)");
                
                // Debug: Show sample results
                foreach (var k in kaizens.Take(3))
                {
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department})");
                    Console.WriteLine($"    Status: '{k.Status}'");
                    Console.WriteLine($"    CostSaving: '{k.CostSaving}'");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    ApprovedBy: '{k.ApprovedBy}'");
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
                    query = query.Where(k => k.Status == status);
                    Console.WriteLine($"Applied status filter: {status}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                Console.WriteLine($"KaizenListManagerDebug returned {kaizens.Count} results");
                
                // Debug: Show sample results with executive filling status
                foreach (var k in kaizens.Take(5))
                {
                    var hasExecutiveFilling = !string.IsNullOrEmpty(k.Category) &&
                                            !string.IsNullOrEmpty(k.ApprovedBy) &&
                                            !string.IsNullOrEmpty(k.Comments) &&
                                            !string.IsNullOrEmpty(k.CanImplementInOtherFields);
                    
                    Console.WriteLine($"  - {k.KaizenNo}: {k.EmployeeName} ({k.Department}) - Has Executive Filling: {hasExecutiveFilling}");
                    Console.WriteLine($"    Category: '{k.Category}'");
                    Console.WriteLine($"    ApprovedBy: '{k.ApprovedBy}'");
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

            // Only allow managers (users without "user" in their username)
            if (IsUserRole())
            {
                return Json(new { success = false, message = "Access denied. Only managers can update status." });
            }

            try
            {
                var kaizen = await _context.KaizenForms.FindAsync(id);
                if (kaizen == null)
                {
                    return Json(new { success = false, message = "Kaizen suggestion not found." });
                }

                kaizen.Status = request.Status;
                
                // If rejecting, save the rejector name
                if (request.Status == "Rejected" && !string.IsNullOrEmpty(request.RejectorName))
                {
                    kaizen.ApprovedBy = request.RejectorName; // Using ApprovedBy field to store rejector name
                }
                
                // If approving, save the approver name
                if (request.Status == "Approved" && !string.IsNullOrEmpty(request.ApproverName))
                {
                    kaizen.ApprovedBy = request.ApproverName; // Using ApprovedBy field to store approver name
                }
                
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Status updated to {request.Status} successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
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
                kaizen.Status = "Implemented"; // You might want to add this status
                
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
                    !string.IsNullOrEmpty(k.ApprovedBy) &&
                    !string.IsNullOrEmpty(k.Comments) &&
                    !string.IsNullOrEmpty(k.CanImplementInOtherFields)
                ).ToList();
                
                Console.WriteLine($"Kaizens with completed executive filling: {kaizensWithExecutiveFilling.Count}");
                
                // Check kaizens by user's department with executive filling
                var userDepartmentKaizens = allKaizens.Where(k => k.Department == userDepartment).ToList();
                Console.WriteLine($"Kaizens in user's department ({userDepartment}): {userDepartmentKaizens.Count}");
                
                var userDepartmentWithExecutiveFilling = userDepartmentKaizens.Where(k => 
                    !string.IsNullOrEmpty(k.Category) &&
                    !string.IsNullOrEmpty(k.ApprovedBy) &&
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
                    Console.WriteLine($"    ApprovedBy: '{k.ApprovedBy}'");
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
                        k.ApprovedBy,
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

                // Validate required fields
                if (string.IsNullOrEmpty(approvedBy?.Trim()))
                {
                    return Json(new { success = false, message = "Please enter your name for signature." });
                }

                if (string.IsNullOrEmpty(comments?.Trim()))
                {
                    return Json(new { success = false, message = "Please provide comments and recommendations." });
                }

                if (string.IsNullOrEmpty(canImplementInOtherFields))
                {
                    return Json(new { success = false, message = "Please select whether this suggestion can be implemented in other fields." });
                }

                // Validate that at least one category is selected
                if (categories == null || categories.Length == 0)
                {
                    return Json(new { success = false, message = "Please select at least one recommended category." });
                }

                // Update the kaizen with executive filling data
                kaizen.Category = categories != null ? string.Join(", ", categories) : "";
                kaizen.ApprovedBy = approvedBy?.Trim();
                kaizen.Comments = comments?.Trim();
                kaizen.CanImplementInOtherFields = canImplementInOtherFields;
                kaizen.ImplementationArea = implementationArea?.Trim();

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Executive review saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }


    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; }
        public string RejectorName { get; set; }
        public string ApproverName { get; set; }
    }

    public class FormBViewModel
    {
        public int Id { get; set; }
        public DateTime ImplementationDate { get; set; }
        public decimal? ImplementationCost { get; set; }
        public string ImplementationDetails { get; set; }
        public string Results { get; set; }
        public string Remarks { get; set; }
    }

    public class ManagerCommentRequest
    {
        public string Comments { get; set; }
        public string Signature { get; set; }
    }
}