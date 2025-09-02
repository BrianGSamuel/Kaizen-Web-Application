using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using KaizenWebApp.Models;
using KaizenWebApp.Data;
using KaizenWebApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace KaizenWebApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ISystemService _systemService;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IWebHostEnvironment env, ISystemService systemService)
        {
            _logger = logger;
            _context = context;
            _env = env;
            _systemService = systemService;
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

            // Check if this is a direct URL access (no referrer or external referrer)
            var referrer = Request.Headers["Referer"].ToString();
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

        // Removed Kaizenform action - it's handled by KaizenController

        public async Task<IActionResult> KaizenList()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            var username = User?.Identity?.Name;

            // Check for kaizen team users first
            if (IsKaizenTeamRole())
            {
                return RedirectToAction("KaizenTeamDashboard", "Kaizen");
            }
            // Check for regular users
            else if (username?.ToLower().Contains("user") == true)
            {
                return RedirectToAction("UserKaizenList", "Kaizen");
            }
            else
            {
                return RedirectToAction("EngineerDashboard", "Kaizen");
            }
        }


        public async Task<IActionResult> Analytics()
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow managers (users without "user" in their username)
            var username = User?.Identity?.Name;
            if (username?.ToLower().Contains("user") == true)
            {
                return RedirectToAction("Kaizenform", "Kaizen");
            }

            try
            {
                var kaizens = await _context.KaizenForms
                    .AsNoTracking()
                    .OrderByDescending(k => k.DateSubmitted)
                    .Take(10) // Show latest 10 kaizens
                    .ToListAsync();

                ViewBag.TotalKaizens = await _context.KaizenForms.CountAsync();
                
                // Total Cost Savings (excluding rejected Kaizens)
                ViewBag.TotalCostSaving = await _context.KaizenForms
                    .Where(k => k.CostSaving.HasValue && k.EngineerStatus != "Rejected")
                    .SumAsync(k => k.CostSaving.Value);
                
                // Total Approved Kaizens
                ViewBag.TotalApprovedKaizens = await _context.KaizenForms
                    .Where(k => k.EngineerStatus == "Approved")
                    .CountAsync();
                
                // Get departments for dropdown (list)
                ViewBag.Departments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                // Get active departments count
                ViewBag.ActiveDepartmentsCount = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .CountAsync();

                return View(kaizens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Kaizen data for Analytics page");
                return View(new List<KaizenForm>());
            }
        }

        public async Task<IActionResult> DepartmentTargetEngineer(int? year, int? month)
        {
            // Check for direct URL access and end session if detected
            if (await CheckAndEndSessionIfDirectAccess())
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow engineers (users without "user", "manager", "admin", "kaizenteam" in their username)
            var username = User?.Identity?.Name;
            if (username?.ToLower().Contains("user") == true || 
                username?.ToLower().Contains("manager") == true || 
                username?.ToLower().Contains("admin") == true || 
                IsKaizenTeamRole())
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Set default year and month if not provided
                year ??= DateTime.Now.Year;
                month ??= DateTime.Now.Month;

                // Get the current engineer's department
                var engineerDepartment = await GetCurrentUserDepartment();
                if (string.IsNullOrEmpty(engineerDepartment))
                {
                    _logger.LogWarning($"No department found for engineer: {username}");
                    return View(new DepartmentTargetsPageViewModel());
                }

                var viewModel = new DepartmentTargetsPageViewModel
                {
                    SelectedYear = year.Value,
                    SelectedMonth = month.Value,
                    AvailableYears = Enumerable.Range(DateTime.Now.Year - 2, 3).ToList(),
                    AvailableMonths = Enumerable.Range(1, 12).ToList(),
                    DepartmentTargets = new List<DepartmentTargetViewModel>()
                };

                // Get department target for the engineer's department in the selected year and month
                var departmentTarget = await _context.DepartmentTargets
                    .Where(dt => dt.Department == engineerDepartment && dt.Year == year && dt.Month == month)
                    .FirstOrDefaultAsync();

                var targetCount = departmentTarget?.TargetCount ?? 0;

                // Count achieved kaizens for the engineer's department in the selected month/year
                var achievedCount = await _context.KaizenForms
                    .Where(k => k.Department == engineerDepartment && 
                               k.DateSubmitted.Year == year && 
                               k.DateSubmitted.Month == month)
                    .CountAsync();

                viewModel.DepartmentTargets.Add(new DepartmentTargetViewModel
                {
                    Department = engineerDepartment,
                    TargetCount = targetCount,
                    AchievedCount = achievedCount,
                    Year = year.Value,
                    Month = month.Value
                });

                viewModel.TotalTarget = viewModel.DepartmentTargets.Sum(dt => dt.TargetCount);
                viewModel.TotalAchieved = viewModel.DepartmentTargets.Sum(dt => dt.AchievedCount);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving department targets for engineer");
                return View(new DepartmentTargetsPageViewModel());
            }
        }

        private async Task<string> GetCurrentUserDepartment()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var username = User.Identity.Name;
                    
                    if (string.IsNullOrEmpty(username))
                    {
                        return null;
                    }
                    
                    var user = await _context.Users
                        .Where(u => u.UserName == username)
                        .FirstOrDefaultAsync();
                    
                    if (user != null)
                    {
                        return user.DepartmentName;
                    }
                    
                    return null;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user department");
                return null;
            }
        }



        public IActionResult SuccessMessage()
        {
            // Check for direct URL access and end session if detected
            if (CheckAndEndSessionIfDirectAccess().Result)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> LandingPage()
        {
            try
            {
                // Get real system statistics from database
                var totalUsers = await _context.Users.CountAsync();
                var totalKaizens = await _context.KaizenForms.CountAsync();
                
                // Calculate total cost savings from approved kaizens (both manager and engineer approved)
                var approvedKaizens = await _context.KaizenForms
                    .Where(k => k.CostSaving.HasValue && 
                               k.EngineerStatus == "Approved" && 
                               k.ManagerStatus == "Approved")
                    .ToListAsync();
                
                var totalCostSaving = approvedKaizens.Sum(k => k.CostSaving.Value);
                
                Console.WriteLine($"Debug: Found {approvedKaizens.Count} approved kaizens");
                foreach (var kaizen in approvedKaizens)
                {
                    Console.WriteLine($"Debug: Kaizen {kaizen.KaizenNo} - CostSaving: {kaizen.CostSaving}, EngineerStatus: {kaizen.EngineerStatus}, ManagerStatus: {kaizen.ManagerStatus}");
                }

                // Get unique departments count
                var uniqueDepartments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .CountAsync();

                // Get active gallery images (optional - table might not exist)
                var galleryImages = new List<Gallery>();
                try
                {
                    galleryImages = await _context.Gallery
                        .Where(g => g.IsActive)
                        .OrderBy(g => g.DisplayOrder)
                        .ThenBy(g => g.UploadDate)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Gallery table not available: {ex.Message}");
                    // Use empty list if Gallery table doesn't exist
                    galleryImages = new List<Gallery>();
                }



                // Set ViewBag values with real database data
                ViewBag.TotalUsers = totalUsers;
                ViewBag.TotalKaizens = totalKaizens;
                ViewBag.TotalCostSaving = totalCostSaving;
                ViewBag.UniqueDepartments = uniqueDepartments;
                ViewBag.GalleryImages = galleryImages;

                Console.WriteLine("LandingPage - Using real database data");
                Console.WriteLine($"ViewBag.TotalUsers: {ViewBag.TotalUsers}");
                Console.WriteLine($"ViewBag.TotalKaizens: {ViewBag.TotalKaizens}");
                Console.WriteLine($"ViewBag.TotalCostSaving: {ViewBag.TotalCostSaving}");
                Console.WriteLine($"ViewBag.UniqueDepartments: {ViewBag.UniqueDepartments}");

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LandingPage action");
                Console.WriteLine($"LandingPage Error: {ex.Message}");
                Console.WriteLine($"LandingPage Stack Trace: {ex.StackTrace}");
                // Return default values if there's an error
                ViewBag.TotalUsers = 0;
                ViewBag.TotalKaizens = 0;
                ViewBag.TotalCostSaving = 0;
                ViewBag.UniqueDepartments = 0;
                ViewBag.GalleryImages = new List<Gallery>();
                return View();
            }
        }

        public IActionResult Index()
        {
            // Redirect to appropriate dashboard based on user role
            var username = User.Identity?.Name;
            if (username?.ToLower() == "admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (IsKaizenTeamRole())
            {
                return RedirectToAction("KaizenTeamDashboard", "Kaizen");
            }
            else if (username?.ToLower().Contains("user") == true)
            {
                return RedirectToAction("Kaizenform", "Kaizen");
            }
            else if (username?.ToLower().Contains("manager") == true)
            {
                return RedirectToAction("KaizenListManager", "Kaizen");
            }
            else
            {
                return RedirectToAction("EngineerDashboard", "Kaizen");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePdfReport(string startDate, string endDate, string department, string status)
        {
            try
            {
                _logger.LogInformation($"=== PDF GENERATION START ===");
                _logger.LogInformation($"PDF Generation Request - StartDate: {startDate}, EndDate: {endDate}, Department: {department}, Status: {status}");
                _logger.LogInformation($"User: {User?.Identity?.Name}");
                _logger.LogInformation($"IsAuthenticated: {User?.Identity?.IsAuthenticated}");
                _logger.LogInformation($"Request Method: {Request.Method}");
                _logger.LogInformation($"Content Type: {Request.ContentType}");

                // Check for direct URL access and end session if detected
                if (await CheckAndEndSessionIfDirectAccess())
                {
                    return RedirectToAction("Login", "Account");
                }

                // Only allow managers (users without "user" in their username)
                var username = User?.Identity?.Name;
                if (username?.ToLower().Contains("user") == true)
                {
                    return Json(new { success = false, message = "Access denied. Only managers can generate reports." });
                }

                // Validate input parameters
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    return Json(new { success = false, message = "Start date and end date are required." });
                }

                // Parse dates
                if (!DateTime.TryParse(startDate, out DateTime start) || !DateTime.TryParse(endDate, out DateTime end))
                {
                    _logger.LogWarning($"Invalid date format - StartDate: {startDate}, EndDate: {endDate}");
                    return Json(new { success = false, message = "Invalid date format." });
                }

                _logger.LogInformation($"Date parsing successful - Start: {start:yyyy-MM-dd}, End: {end:yyyy-MM-dd}");

                // Build query
                var query = _context.KaizenForms.AsQueryable();

                // Apply date filter
                query = query.Where(k => k.DateSubmitted >= start && k.DateSubmitted <= end);
                _logger.LogInformation($"Applied date filter: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");

                // Apply department filter
                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(k => k.Department == department);
                    _logger.LogInformation($"Applied department filter: {department}");
                }

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(k => k.EngineerStatus == status);
                    _logger.LogInformation($"Applied engineer status filter: {status}");
                }

                var kaizens = await query.OrderByDescending(k => k.DateSubmitted).ToListAsync();
                _logger.LogInformation($"Found {kaizens.Count} kaizens matching criteria");

                if (!kaizens.Any())
                {
                    return Json(new { success = false, message = "No kaizen suggestions found for the selected criteria." });
                }

                // Generate PDF
                _logger.LogInformation("Starting PDF generation...");
                byte[] pdfBytes;
                try
                {
                    pdfBytes = GeneratePdfReport(kaizens, start, end, department, status);
                    _logger.LogInformation($"PDF generated successfully, size: {pdfBytes.Length} bytes");
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError(pdfEx, "Error generating PDF content");
                    return Json(new { success = false, message = "Error generating PDF content. Please try again." });
                }

                var fileName = $"Kaizen_Report_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf";
                _logger.LogInformation($"PDF filename: {fileName}");

                // Save to wwwroot/reports directory
                var reportsDir = Path.Combine(_env.WebRootPath, "reports");
                Directory.CreateDirectory(reportsDir);
                var filePath = Path.Combine(reportsDir, fileName);
                
                _logger.LogInformation($"Saving PDF to: {filePath}");
                try
                {
                    await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                    _logger.LogInformation("PDF saved successfully");
                }
                catch (Exception fileEx)
                {
                    _logger.LogError(fileEx, "Error saving PDF file");
                    return Json(new { success = false, message = "Error saving PDF file. Please try again." });
                }

                var fileUrl = $"/reports/{fileName}";
                _logger.LogInformation($"PDF URL: {fileUrl}");

                _logger.LogInformation($"=== PDF GENERATION SUCCESS ===");
                return Json(new { success = true, fileUrl = fileUrl, fileName = fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                _logger.LogInformation($"=== PDF GENERATION FAILED ===");
                return Json(new { success = false, message = $"Error generating PDF: {ex.Message}" });
            }
        }



        private byte[] GeneratePdfReport(List<KaizenForm> kaizens, DateTime startDate, DateTime endDate, string department, string status)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                document.Open();

                // Add title
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                Paragraph title = new Paragraph("Kaizen Suggestions Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20f;
                document.Add(title);

                // Add report criteria
                Font criteriaFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                Paragraph criteria = new Paragraph();
                criteria.Add(new Chunk("Report Period: ", criteriaFont));
                criteria.Add(new Chunk($"{startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy}", criteriaFont));
                criteria.Add(new Chunk("\n", criteriaFont));
                
                if (!string.IsNullOrEmpty(department))
                {
                    criteria.Add(new Chunk("Department: ", criteriaFont));
                    criteria.Add(new Chunk(department, criteriaFont));
                    criteria.Add(new Chunk("\n", criteriaFont));
                }
                
                if (!string.IsNullOrEmpty(status))
                {
                    criteria.Add(new Chunk("Engineer Status: ", criteriaFont));
                    criteria.Add(new Chunk(status, criteriaFont));
                    criteria.Add(new Chunk("\n", criteriaFont));
                }
                
                criteria.Add(new Chunk($"Total Suggestions: {kaizens.Count}", criteriaFont));
                criteria.SpacingAfter = 20f;
                document.Add(criteria);

                // Create table
                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;

                // Add headers
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Date", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });
                table.AddCell(new PdfPCell(new Phrase("Kaizen No", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });
                table.AddCell(new PdfPCell(new Phrase("Department", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });
                table.AddCell(new PdfPCell(new Phrase("Employee", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });
                table.AddCell(new PdfPCell(new Phrase("Engineer Status", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });
                table.AddCell(new PdfPCell(new Phrase("Cost Saving", headerFont)) { BackgroundColor = new BaseColor(200, 200, 200) });

                // Add data
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                foreach (var kaizen in kaizens)
                {
                    table.AddCell(new PdfPCell(new Phrase(kaizen.DateSubmitted.ToString("MMM dd, yyyy"), dataFont)));
                    table.AddCell(new PdfPCell(new Phrase(kaizen.KaizenNo, dataFont)));
                    table.AddCell(new PdfPCell(new Phrase(kaizen.Department ?? "N/A", dataFont)));
                    table.AddCell(new PdfPCell(new Phrase($"{kaizen.EmployeeName} ({kaizen.EmployeeNo})", dataFont)));
                    table.AddCell(new PdfPCell(new Phrase(kaizen.EngineerStatus ?? "Pending", dataFont)));
                    table.AddCell(new PdfPCell(new Phrase(kaizen.CostSaving.HasValue ? $"${kaizen.CostSaving.Value:N2}" : "N/A", dataFont)));
                }

                document.Add(table);

                // Add summary statistics
                document.Add(new Paragraph("\n", criteriaFont));
                var totalCostSaving = kaizens.Where(k => k.CostSaving.HasValue).Sum(k => k.CostSaving.Value);
                var approvedCount = kaizens.Count(k => k.EngineerStatus == "Approved");
                var pendingCount = kaizens.Count(k => k.EngineerStatus == "Pending" || string.IsNullOrEmpty(k.EngineerStatus));
                var rejectedCount = kaizens.Count(k => k.EngineerStatus == "Rejected");

                Paragraph summary = new Paragraph();
                summary.Add(new Chunk("Summary Statistics:\n", headerFont));
                summary.Add(new Chunk($"Total Cost Savings: ${totalCostSaving:N2}\n", criteriaFont));
                summary.Add(new Chunk($"Approved: {approvedCount}\n", criteriaFont));
                summary.Add(new Chunk($"Pending: {pendingCount}\n", criteriaFont));
                summary.Add(new Chunk($"Rejected: {rejectedCount}\n", criteriaFont));
                summary.SpacingAfter = 20f;
                document.Add(summary);

                document.Close();
                return ms.ToArray();
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Maintenance()
        {
            var maintenance = await _systemService.GetSystemMaintenanceStatusAsync();
            ViewBag.MaintenanceMessage = maintenance.MaintenanceMessage;
            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> TestLandingPage()
        {
            try
            {
                // Test basic database connection
                var totalUsers = await _context.Users.CountAsync();
                var totalKaizens = await _context.KaizenForms.CountAsync();
                
                // Test cost savings calculation
                var totalCostSaving = await _context.KaizenForms
                    .Where(k => k.CostSaving.HasValue && 
                               k.EngineerStatus == "Approved" && 
                               k.ManagerStatus == "Approved")
                    .SumAsync(k => k.CostSaving.Value);

                // Test departments count
                var uniqueDepartments = await _context.KaizenForms
                    .Where(k => !string.IsNullOrEmpty(k.Department))
                    .Select(k => k.Department)
                    .Distinct()
                    .CountAsync();

                // Test if Gallery table exists
                var galleryCount = 0;
                try
                {
                    galleryCount = await _context.Gallery.CountAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Gallery table error: {ex.Message}");
                }



                var result = new
                {
                    TotalUsers = totalUsers,
                    TotalKaizens = totalKaizens,
                    TotalCostSaving = totalCostSaving,
                    UniqueDepartments = uniqueDepartments,
                    GalleryCount = galleryCount,
                    Success = true
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { 
                    error = ex.Message, 
                    stackTrace = ex.StackTrace,
                    Success = false 
                });
            }
        }

        [AllowAnonymous]
        public IActionResult SimpleTest()
        {
            ViewBag.TotalUsers = 9;
            ViewBag.TotalKaizens = 8;
            ViewBag.TotalCostSaving = 40500;
            ViewBag.UniqueDepartments = 2;
            
            return View();
        }

    }
}
