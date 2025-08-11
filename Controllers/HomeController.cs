using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using KaizenWebApp.Models;
using KaizenWebApp.Data;
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

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IWebHostEnvironment env)
        {
            _logger = logger;
            _context = context;
            _env = env;
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
            if (username?.ToLower().Contains("kaizenteam") == true)
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
                return RedirectToAction("KaizenListEngineer", "Kaizen");
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

        public IActionResult Index()
        {
            // Redirect to appropriate dashboard based on user role
            var username = User.Identity?.Name;
            if (username?.ToLower() == "admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (username?.ToLower().Contains("kaizenteam") == true)
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
                return RedirectToAction("KaizenListEngineer", "Kaizen");
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

    }
}
