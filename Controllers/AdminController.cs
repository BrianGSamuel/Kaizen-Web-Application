using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using System.Linq;
using System;
using System.Collections.Generic; // Added for List

namespace KaizenWebApp.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            Console.WriteLine($"Admin Dashboard accessed by user: {username}");
            
            if (username?.ToLower() != "admin")
            {
                Console.WriteLine($"Access denied for user: {username}");
                return RedirectToAction("AccessDenied", "Home");
            }

            // Get statistics for dashboard
            var totalUsers = _context.Users.Count();
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

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalKaizens = totalKaizens;
            ViewBag.PendingKaizens = pendingKaizens;
            ViewBag.RejectedKaizens = rejectedKaizens;
            ViewBag.ApprovedKaizens = approvedKaizens;

            return View();
        }

        public IActionResult UserManagement()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var users = _context.Users.ToList();
            return View(users);
        }

        public IActionResult DepartmentTargets(int? year, int? month, string statusFilter)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

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
            var departmentTargets = _context.DepartmentTargets
                .Where(dt => dt.Year == selectedYear && dt.Month == selectedMonth)
                .ToList();

            // Get all departments from kaizen forms
            var allDepartments = _context.KaizenForms
                .Where(k => k.Department != null && k.Department.Trim() != "")
                .Select(k => k.Department)
                .Distinct()
                .ToList();

            foreach (var department in allDepartments)
            {
                var target = departmentTargets.FirstOrDefault(dt => dt.Department == department);
                var targetCount = target?.TargetCount ?? 0;

                // Count achieved kaizens for this department in the selected month/year
                var achievedCount = _context.KaizenForms
                    .Count(k => k.Department == department && 
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
            var departmentSubmissions = _context.KaizenForms
                .Where(k => k.Department != null && k.Department.Trim() != "")
                .GroupBy(k => k.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            // Calculate most cost saving department
            var costSavingDepartments = _context.KaizenForms
                .Where(k => k.Department != null && k.Department.Trim() != "" && k.CostSaving.HasValue && k.CostSaving > 0)
                .GroupBy(k => k.Department)
                .Select(g => new { Department = g.Key, TotalSaving = g.Sum(k => k.CostSaving.Value) })
                .OrderByDescending(x => x.TotalSaving)
                .FirstOrDefault();

            ViewBag.MostSubmittedDepartment = departmentSubmissions?.Department ?? "No Data";
            ViewBag.MostSubmittedCount = departmentSubmissions?.Count ?? 0;
            ViewBag.MostCostSavingDepartment = costSavingDepartments?.Department ?? "No Data";
            ViewBag.MostCostSavingAmount = costSavingDepartments?.TotalSaving ?? 0;

            // Calculate most cost saving individual kaizen
            var mostCostSavingKaizen = _context.KaizenForms
                .Where(k => k.CostSaving.HasValue && k.CostSaving > 0)
                .OrderByDescending(k => k.CostSaving)
                .Select(k => new { 
                    KaizenNo = k.KaizenNo, 
                    CostSavingAmount = k.CostSaving.Value, 
                    Department = k.Department 
                })
                .FirstOrDefault();

            ViewBag.MostCostSavingKaizenNo = mostCostSavingKaizen?.KaizenNo ?? "No Data";
            ViewBag.MostCostSavingKaizenAmount = mostCostSavingKaizen?.CostSavingAmount ?? 0;
            ViewBag.MostCostSavingKaizenDepartment = mostCostSavingKaizen?.Department ?? "No Data";

            return View(viewModel);
        }

        public IActionResult AwardTracking(string startDate, string endDate, string department, string category, string awardStatus)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
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

            return View(approvedKaizens);
        }

        public IActionResult AwardDetails(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == id);
            if (kaizen == null)
            {
                return NotFound();
            }

            return View(kaizen);
        }

        [HttpPost]
        public IActionResult AssignAward(int kaizenId, string awardPrice, string committeeComments, string committeeSignature)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var kaizen = _context.KaizenForms.FirstOrDefault(k => k.Id == kaizenId);
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
            return RedirectToAction("AwardTracking");
        }

        [HttpPost]
        public IActionResult SetDepartmentTarget(string department, int targetCount, int year, int month, string notes = "")
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var existingTarget = _context.DepartmentTargets
                .FirstOrDefault(dt => dt.Department == department && dt.Year == year && dt.Month == month);

            if (existingTarget != null)
            {
                existingTarget.TargetCount = targetCount;
                existingTarget.Notes = notes;
                existingTarget.UpdatedAt = DateTime.Now;
            }
            else
            {
                var newTarget = new DepartmentTarget
                {
                    Department = department,
                    TargetCount = targetCount,
                    Year = year,
                    Month = month,
                    Notes = notes
                };
                _context.DepartmentTargets.Add(newTarget);
            }

            _context.SaveChanges();

            return RedirectToAction("DepartmentTargets", new { year, month });
        }

        // User Management Actions
        [HttpGet]
        public IActionResult GetUserDetails(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var role = "User";
            if (user.UserName?.ToLower().Contains("admin") == true)
            {
                role = "Administrator";
            }
            else if (user.UserName?.ToLower().Contains("engineer") == true)
            {
                role = "Engineer";
            }
            else if (user.UserName?.ToLower().Contains("manager") == true)
            {
                role = "Manager";
            }
            else if (user.UserName?.ToLower().Contains("kaizenteam") == true)
            {
                role = "Kaizen Team";
            }

            var userDetails = new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    departmentName = user.DepartmentName,
                    role = role
                }
            };

            return Json(userDetails);
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View("EditUser", user);
        }

        [HttpPost]
        public IActionResult EditUser(Users model)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
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
                return View(model);
            }

            user.UserName = model.UserName;
            user.DepartmentName = model.DepartmentName;
            
            // Only update password if a new one is provided
            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = model.Password;
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = "User updated successfully!";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
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

        // New action for viewing all kaizens with comprehensive filters
        public IActionResult ViewAllKaizens(string searchString, string department, string status, 
            string startDate, string endDate, string category, string engineerStatus, string managerStatus, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var query = _context.KaizenForms.AsQueryable();

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
            }

            // Apply category filter
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(k => k.Category != null && k.Category.Contains(category));
            }

            // Apply engineer status filter
            if (!string.IsNullOrEmpty(engineerStatus))
            {
                query = query.Where(k => (k.EngineerStatus ?? "Pending") == engineerStatus);
            }

            // Apply manager status filter
            if (!string.IsNullOrEmpty(managerStatus))
            {
                query = query.Where(k => (k.ManagerStatus ?? "Pending") == managerStatus);
            }

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
            }

            // Apply date range filter
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime start))
            {
                query = query.Where(k => k.DateSubmitted >= start);
            }

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime end))
            {
                end = end.AddDays(1);
                query = query.Where(k => k.DateSubmitted < end);
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

            // Get filtered results
            var kaizens = query.OrderByDescending(k => k.DateSubmitted).ToList();

            // Populate ViewBag with filter options
            ViewBag.Departments = _context.KaizenForms
                .Where(k => !string.IsNullOrEmpty(k.Department))
                .Select(k => k.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Get all unique categories
            var allCategories = new List<string>();
            var kaizensWithCategories = _context.KaizenForms
                .Where(k => !string.IsNullOrEmpty(k.Category))
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

            // Pass current filter values back to view
            ViewBag.CurrentFilters = new
            {
                SearchString = searchString,
                Department = department,
                Status = status,
                StartDate = startDate,
                EndDate = endDate,
                Category = category,
                EngineerStatus = engineerStatus,
                ManagerStatus = managerStatus,
                CostSavingRange = costSavingRange,
                EmployeeName = employeeName,
                EmployeeNo = employeeNo,
                KaizenNo = kaizenNo
            };

            return View(kaizens);
        }

        // AJAX endpoint for real-time search
        [HttpGet]
        public IActionResult SearchAllKaizens(string searchString, string department, string status, 
            string startDate, string endDate, string category, string engineerStatus, string managerStatus, 
            string costSavingRange, string employeeName, string employeeNo, string kaizenNo)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return Json(new { success = false, message = "Access denied." });
            }

            try
            {
                var query = _context.KaizenForms.AsQueryable();

                // Apply all filters (same logic as ViewAllKaizens)
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

                if (!string.IsNullOrEmpty(engineerStatus))
                {
                    query = query.Where(k => (k.EngineerStatus ?? "Pending") == engineerStatus);
                }

                if (!string.IsNullOrEmpty(managerStatus))
                {
                    query = query.Where(k => (k.ManagerStatus ?? "Pending") == managerStatus);
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

                var kaizens = query.OrderByDescending(k => k.DateSubmitted)
                    .Select(k => new
                    {
                        id = k.Id,
                        kaizenNo = k.KaizenNo,
                        employeeName = k.EmployeeName,
                        employeeNo = k.EmployeeNo,
                        department = k.Department,
                        dateSubmitted = k.DateSubmitted.ToString("yyyy-MM-dd"),
                        costSaving = k.CostSaving,
                        engineerStatus = k.EngineerStatus ?? "Pending",
                        managerStatus = k.ManagerStatus ?? "Pending",
                        category = k.Category,
                        suggestionDescription = k.SuggestionDescription
                    })
                    .ToList();

                return Json(new { success = true, kaizens = kaizens });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error searching kaizens: " + ex.Message });
            }
        }
    }
} 