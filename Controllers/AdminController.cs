using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using KaizenWebApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Collections.Generic; // Added for List
using System.Text; // Added for StringBuilder
using System.IO; // Added for File operations

namespace KaizenWebApp.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISystemService _systemService;

        public AdminController(AppDbContext context, ISystemService systemService)
        {
            _context = context;
            _systemService = systemService;
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

            // Calculate awarded kaizens
            // Count kaizens with scores (dynamic award calculation)
            var awardedKaizens = _context.KaizenForms.Count(k => 
                k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved" && 
                _context.KaizenMarkingScores.Any(s => s.KaizenId == k.Id));

            // Calculate percentages
            var totalForPercentage = totalKaizens > 0 ? totalKaizens : 1;
            var pendingPercentage = (int)((double)pendingKaizens / totalForPercentage * 100);
            var approvedPercentage = (int)((double)approvedKaizens / totalForPercentage * 100);
            var rejectedPercentage = (int)((double)rejectedKaizens / totalForPercentage * 100);

            // Get most active department
            var mostActiveDepartment = _context.KaizenForms
                .Where(k => !string.IsNullOrEmpty(k.Department))
                .GroupBy(k => k.Department)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .FirstOrDefault();

            // Get department with highest cost saving
            var highestCostSavingDept = _context.KaizenForms
                .Where(k => k.CostSaving.HasValue && k.CostSaving > 0)
                .GroupBy(k => k.Department)
                .OrderByDescending(g => g.Sum(k => k.CostSaving.Value))
                .Select(g => new { Department = g.Key, TotalSaving = g.Sum(k => k.CostSaving.Value) })
                .FirstOrDefault();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalKaizens = totalKaizens;
            ViewBag.PendingKaizens = pendingKaizens;
            ViewBag.RejectedKaizens = rejectedKaizens;
            ViewBag.ApprovedKaizens = approvedKaizens;
            ViewBag.AwardedKaizens = awardedKaizens;
            ViewBag.PendingPercentage = pendingPercentage;
            ViewBag.ApprovedPercentage = approvedPercentage;
            ViewBag.RejectedPercentage = rejectedPercentage;
            ViewBag.MostActiveDepartment = mostActiveDepartment?.Department ?? "N/A";
            ViewBag.MostActiveCount = mostActiveDepartment?.Count ?? 0;
            ViewBag.HighestCostSaving = highestCostSavingDept?.TotalSaving ?? 0;
            ViewBag.HighestCostSavingDepartment = highestCostSavingDept?.Department ?? "N/A";

            return View();
        }

        public IActionResult UserManagement()
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin" && !username?.ToLower().Contains("kaizenteam") == true)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var users = _context.Users.ToList();
            
            // If kaizen team user, return the kaizen team view
            if (username?.ToLower().Contains("kaizenteam") == true)
            {
                return View("KaizenTeamUserManagement", users);
            }
            
            return View(users);
        }

        public IActionResult DepartmentTargets(int? year, int? month, string departmentSearch)
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

        public IActionResult AwardTracking(string startDate, string endDate, string department, string awardStatus)
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


            // Note: Award status filtering will be applied after dynamic calculation

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


        public async Task<IActionResult> AwardDetails(int id)
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

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult AssignAward(int kaizenId, string committeeComments = "", string committeeSignature = "")
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            try
            {
                // Log all form data for debugging
                Console.WriteLine("=== FORM DATA DEBUG ===");
                Console.WriteLine($"Kaizen ID: {kaizenId}");
                Console.WriteLine($"Committee Comments: '{committeeComments}'");
                Console.WriteLine($"Committee Signature: '{committeeSignature}'");
                
                // Log all form keys
                Console.WriteLine("=== ALL FORM KEYS ===");
                foreach (var key in Request.Form.Keys)
                {
                    Console.WriteLine($"Form Key: {key} = '{Request.Form[key]}'");
                }

                

                // Find the kaizen record using raw SQL to avoid entity tracking issues
                var kaizen = _context.KaizenForms.FromSqlRaw("SELECT * FROM KaizenForms WHERE Id = {0}", kaizenId).FirstOrDefault();
                if (kaizen == null)
                {
                    Console.WriteLine($"ERROR: Kaizen with ID {kaizenId} not found");
                    return NotFound();
                }

                Console.WriteLine($"Found Kaizen: {kaizen.KaizenNo}");

                // Update the kaizen record using raw SQL to avoid entity tracking issues
                var updateResult = _context.Database.ExecuteSqlRaw(
                    "UPDATE KaizenForms SET CommitteeComments = {0}, CommitteeSignature = {1}, AwardDate = {2} WHERE Id = {3}",
                    committeeComments ?? "",
                    committeeSignature ?? "",
                    DateTime.Now,
                    kaizenId
                );

                Console.WriteLine($"Kaizen update result: {updateResult} rows affected");
                Console.WriteLine($"Updated Kaizen fields: Comments={committeeComments}, Signature={committeeSignature}");

                // Save marking criteria scores using raw SQL
                var form = Request.Form;
                var markingCriteria = _context.MarkingCriteria.Where(c => c.IsActive).ToList();
                var savedScores = new List<string>();
                
                Console.WriteLine($"Found {markingCriteria.Count} active marking criteria");
                
                foreach (var criteria in markingCriteria)
                {
                    var scoreKey = $"criteriaScore_{criteria.Id}";
                    Console.WriteLine($"Checking for score key: {scoreKey}");
                    
                    if (form.ContainsKey(scoreKey))
                    {
                        var scoreValue = form[scoreKey].ToString();
                        Console.WriteLine($"Found score value: '{scoreValue}' for criteria {criteria.CriteriaName}");
                        
                        if (int.TryParse(scoreValue, out int score))
                        {
                            // Accept any score value (0 or higher)
                            if (score < 0)
                            {
                                score = 0;
                            }

                            // Check if score already exists for this kaizen and criteria using raw SQL
                            var existingScore = _context.KaizenMarkingScores
                                .FromSqlRaw("SELECT * FROM KaizenMarkingScores WHERE KaizenId = {0} AND MarkingCriteriaId = {1}", kaizenId, criteria.Id)
                                .FirstOrDefault();
                            
                            if (existingScore != null)
                            {
                                // Update existing score using raw SQL
                                var updateScoreResult = _context.Database.ExecuteSqlRaw(
                                    "UPDATE KaizenMarkingScores SET Score = {0}, CreatedAt = {1}, CreatedBy = {2} WHERE KaizenId = {3} AND MarkingCriteriaId = {4}",
                                    score, DateTime.Now, username, kaizenId, criteria.Id
                                );
                                savedScores.Add($"Updated {criteria.CriteriaName}: {score}/{criteria.Weight}");
                                Console.WriteLine($"Updated existing score for {criteria.CriteriaName}: {score} (result: {updateScoreResult})");
                            }
                            else
                            {
                                // Create new score using raw SQL
                                var insertScoreResult = _context.Database.ExecuteSqlRaw(
                                    "INSERT INTO KaizenMarkingScores (KaizenId, MarkingCriteriaId, Score, CreatedAt, CreatedBy) VALUES ({0}, {1}, {2}, {3}, {4})",
                                    kaizenId, criteria.Id, score, DateTime.Now, username
                                );
                                savedScores.Add($"Added {criteria.CriteriaName}: {score}/{criteria.Weight}");
                                Console.WriteLine($"Added new score for {criteria.CriteriaName}: {score} (result: {insertScoreResult})");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"ERROR: Could not parse score value '{scoreValue}' as integer, setting to 0");
                            // Create a score of 0 if parsing fails using raw SQL
                            var insertScoreResult = _context.Database.ExecuteSqlRaw(
                                "INSERT INTO KaizenMarkingScores (KaizenId, MarkingCriteriaId, Score, CreatedAt, CreatedBy) VALUES ({0}, {1}, {2}, {3}, {4})",
                                kaizenId, criteria.Id, 0, DateTime.Now, username
                            );
                            savedScores.Add($"Added {criteria.CriteriaName}: 0/{criteria.Weight} (default)");
                            Console.WriteLine($"Added default score for {criteria.CriteriaName}: 0 (result: {insertScoreResult})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Score key {scoreKey} not found in form data, creating default score of 0");
                        // Create a default score of 0 if the key is not found using raw SQL
                        var insertScoreResult = _context.Database.ExecuteSqlRaw(
                            "INSERT INTO KaizenMarkingScores (KaizenId, MarkingCriteriaId, Score, CreatedAt, CreatedBy) VALUES ({0}, {1}, {2}, {3}, {4})",
                            kaizenId, criteria.Id, 0, DateTime.Now, username
                        );
                        savedScores.Add($"Added {criteria.CriteriaName}: 0/{criteria.Weight} (default)");
                        Console.WriteLine($"Added default score for {criteria.CriteriaName}: 0 (result: {insertScoreResult})");
                    }
                }

                Console.WriteLine($"Saving {savedScores.Count} scores: {string.Join(", ", savedScores)}");

                // Verify the data was saved by retrieving it using raw SQL
                var savedKaizen = _context.KaizenForms.FromSqlRaw("SELECT * FROM KaizenForms WHERE Id = {0}", kaizenId).FirstOrDefault();
                var savedScoresCount = _context.KaizenMarkingScores.FromSqlRaw("SELECT * FROM KaizenMarkingScores WHERE KaizenId = {0}", kaizenId).Count();
                
                Console.WriteLine($"Verification - Saved Award Price: [Dynamic calculation based on scores]");
                Console.WriteLine($"Verification - Saved Comments: {savedKaizen?.CommitteeComments}");
                Console.WriteLine($"Verification - Saved Signature: {savedKaizen?.CommitteeSignature}");
                Console.WriteLine($"Verification - Saved Scores Count: {savedScoresCount}");

                TempData["SubmissionSuccessMessage"] = $"Marks assigned successfully! Scores saved: {savedScoresCount} criteria.";
                return RedirectToAction("AwardDetails", new { id = kaizenId });
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"ERROR assigning award for Kaizen ID: {kaizenId}");
                Console.WriteLine($"Error Message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                TempData["SubmissionErrorMessage"] = $"An error occurred while assigning the award: {ex.Message}";
                return RedirectToAction("AwardDetails", new { id = kaizenId });
            }
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

            TempData["SubmissionSuccessMessage"] = "Department target saved successfully!";
            return RedirectToAction("DepartmentTargets", new { year, month });
        }

        // User Management Actions
        [HttpGet]
        public IActionResult GetUserDetails(int id)
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin" && !username?.ToLower().Contains("kaizenteam") == true)
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
                    plant = user.Plant,
                    role = roleDisplay,
                    employeeName = user.EmployeeName,
                    employeeNumber = user.EmployeeNumber,
                    employeePhotoPath = user.EmployeePhotoPath
                }
            };

            return Json(userDetails);
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin" && !username?.ToLower().Contains("kaizenteam") == true)
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
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin" && !username?.ToLower().Contains("kaizenteam") == true)
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

            // Update all user fields (except UserName which is readonly and EmployeePhotoPath which is removed from form)
            user.DepartmentName = model.DepartmentName;
            user.Plant = model.Plant;
            user.Role = model.Role;
            user.EmployeeName = model.EmployeeName;
            user.EmployeeNumber = model.EmployeeNumber;
            
            // Only update password if a new one is provided
            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = model.Password;
            }

            _context.SaveChanges();

            TempData["SubmissionSuccessMessage"] = "User updated successfully!";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public IActionResult AddUser(string username, string department, string password)
        {
            // Check if user is admin or kaizen team
            var currentUser = User.Identity?.Name;
            if (currentUser?.ToLower() != "admin" && !currentUser?.ToLower().Contains("kaizenteam") == true)
            {
                return Json(new { success = false, message = "Access denied." });
            }

            try
            {
                // Check if username already exists
                var existingUser = _context.Users.FirstOrDefault(u => u.UserName == username);
                if (existingUser != null)
                {
                    return Json(new { success = false, message = "Username already exists." });
                }

                var newUser = new Users
                {
                    UserName = username,
                    DepartmentName = department,
                    Password = password
                };

                _context.Users.Add(newUser);
                _context.SaveChanges();

                return Json(new { success = true, message = "User added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding user: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ExportUsers()
        {
            // Check if user is admin or kaizen team
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin" && !username?.ToLower().Contains("kaizenteam") == true)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var users = _context.Users.ToList();
            
            // Create CSV content
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Username,Department,Role");
            
            foreach (var user in users)
            {
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
                
                csv.AppendLine($"{user.UserName},{user.DepartmentName},{role}");
            }
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "users.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                
                if (string.IsNullOrEmpty(username) || username.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied. Only administrators can delete users." });
                }

                // Check if user exists
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

                // Delete the user
                _context.Users.Remove(user);
                _context.SaveChanges();
                
                return Json(new { success = true, message = "User deleted successfully." });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                return Json(new { success = false, message = "Cannot delete user. This user may have related data in the system." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while deleting the user. Please try again." });
            }
        }



        // New action for viewing all kaizens with comprehensive filters
        public IActionResult ViewAllKaizens(string searchString, string department, string status, 
            string startDate, string endDate, string engineerStatus, string managerStatus, 
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


            // Pass current filter values back to view
            ViewBag.CurrentFilters = new
            {
                Department = department,
                Status = status,
                StartDate = startDate,
                EndDate = endDate,
                EngineerStatus = engineerStatus,
                ManagerStatus = managerStatus,
                CostSavingRange = costSavingRange,
                EmployeeName = employeeName,
                EmployeeNo = employeeNo,
                KaizenNo = kaizenNo
            };

            return View(kaizens);
        }

        // GET: /Admin/AdminKaizenDetails - Admin kaizen details page
        [HttpGet]
        public async Task<IActionResult> AdminKaizenDetails(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
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
                    return RedirectToAction("ViewAllKaizens");
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

                return View("~/Views/Admin/AdminKaizenDetails.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AdminKaizenDetails: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the kaizen details.";
                return RedirectToAction("ViewAllKaizens");
            }
        }

        // AJAX endpoint for real-time search
        [HttpGet]
        public IActionResult SearchAllKaizens(string searchString, string department, string status, 
            string startDate, string endDate, string engineerStatus, string managerStatus, 
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
                }

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

        public async Task<IActionResult> Settings()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var maintenance = await _systemService.GetSystemMaintenanceStatusAsync();
            var users = await _context.Users.ToListAsync();

            var viewModel = new SettingsViewModel
            {
                IsSystemOffline = maintenance.IsSystemOffline,
                MaintenanceMessage = maintenance.MaintenanceMessage,
                MaintenanceStartTime = maintenance.MaintenanceStartTime,
                MaintenanceEndTime = maintenance.MaintenanceEndTime,
                AvailableUsers = users
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSystemMaintenance(SettingsViewModel model)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            if (await _systemService.SetSystemMaintenanceStatusAsync(model.IsSystemOffline, model.MaintenanceMessage, username))
            {
                TempData["SuccessMessage"] = model.IsSystemOffline 
                    ? "System has been put into maintenance mode." 
                    : "System has been brought back online.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update system maintenance status.";
            }

            return RedirectToAction("Settings");
        }

        [HttpPost]
        public async Task<IActionResult> SendNotification(SettingsViewModel model)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            if (ModelState.IsValid)
            {
                if (await _systemService.SendNotificationAsync(model, username))
                {
                    TempData["SuccessMessage"] = "Notification sent successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to send notification.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please check the notification details.";
            }

            return RedirectToAction("Settings");
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            var role = user?.Role ?? "User";

            var notifications = await _systemService.GetNotificationsForUserAsync(username, role);
            var unreadCount = await _systemService.GetUnreadNotificationCountAsync(username, role);

            return Json(new { 
                success = true, 
                notifications = notifications.Select(n => new {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.NotificationType,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt.ToString("MMM dd, yyyy HH:mm")
                }),
                unreadCount = unreadCount
            });
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationAsRead(int notificationId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var success = await _systemService.MarkNotificationAsReadAsync(notificationId, username);
            return Json(new { success = success });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteNotification([FromBody] DeleteNotificationRequest request)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var success = await _systemService.DeleteNotificationAsync(request.NotificationId, username);
            return Json(new { success = success, message = success ? "Notification deleted successfully" : "Failed to delete notification" });
        }

        [HttpGet]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return Json(new { success = false, message = "Access denied" });
            }

            try
            {
                var users = await _context.Users
                    .Where(u => u.Role == role)
                    .Select(u => new { 
                        username = u.UserName, 
                        displayName = $"{u.UserName} ({u.Role})" 
                    })
                    .ToListAsync();

                return Json(new { success = true, users = users });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error retrieving users: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLastNotification()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            if (await _systemService.DeleteLastNotificationAsync())
            {
                TempData["SuccessMessage"] = "Last notification deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete last notification or no notifications found.";
            }

            return RedirectToAction("Settings");
        }

        // MARKING CRITERIA MANAGEMENT METHODS

        [HttpGet]
        public async Task<IActionResult> MarkingCriteria()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var criteria = await _context.MarkingCriteria
                .OrderBy(c => c.Category)
                .ThenBy(c => c.CriteriaName)
                .ToListAsync();

            var thresholds = await _context.AwardThresholds
                .OrderBy(t => t.MinPercentage)
                .ToListAsync();

            var viewModel = new MarkingCriteriaManagementViewModel
            {
                MarkingCriteria = criteria,
                AwardThresholds = thresholds
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> AddMarkingCriteria()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Load existing criteria
            var existingCriteria = await _context.MarkingCriteria.Where(c => c.IsActive).ToListAsync();
            return View(existingCriteria);
        }

        [HttpPost]
        public async Task<IActionResult> AddMarkingCriteria([FromBody] List<MarkingCriteriaViewModel> models)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                if (models == null || !models.Any())
                {
                    return Json(new { success = false, message = "No criteria provided" });
                }

                // Validate individual criteria weights are within valid range
                foreach (var model in models)
                {
                    if (model.Weight <= 0 || model.Weight > 100)
                    {
                        return Json(new { success = false, message = $"Weight must be between 1 and 100, but got {model.Weight}%" });
                    }
                }

                var criteriaList = new List<MarkingCriteria>();
                foreach (var model in models)
                {
                    var criteria = new MarkingCriteria
                    {
                        CriteriaName = model.CriteriaName,
                        Description = model.Description ?? $"Evaluation criteria for {model.CriteriaName}",
                        MaxScore = model.MaxScore,
                        Weight = model.Weight,
                        Category = model.Category ?? "General",
                        IsActive = model.IsActive,
                        Notes = model.Notes,
                        CreatedBy = username,
                        CreatedAt = DateTime.Now
                    };

                    criteriaList.Add(criteria);
                }

                _context.MarkingCriteria.AddRange(criteriaList);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"{criteriaList.Count} criteria added successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error saving criteria: {ex.Message}" });
            }
        }







        [HttpPost]
        public async Task<IActionResult> DeleteMarkingCriteria([FromBody] DeleteMarkingCriteriaRequest request)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var criteria = await _context.MarkingCriteria.FindAsync(request.Id);
                if (criteria == null)
                {
                    return Json(new { success = false, message = "Criteria not found" });
                }

                _context.MarkingCriteria.Remove(criteria);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Criteria deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting criteria: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleMarkingCriteriaStatus(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return Json(new { success = false, message = "Access denied" });
            }

            var criteria = await _context.MarkingCriteria.FindAsync(id);
            if (criteria == null)
            {
                return Json(new { success = false, message = "Criteria not found" });
            }

            criteria.IsActive = !criteria.IsActive;
            criteria.UpdatedBy = username;
            criteria.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Criteria {(criteria.IsActive ? "activated" : "deactivated")} successfully",
                isActive = criteria.IsActive
            });
        }

        // Category Management Methods
        public async Task<IActionResult> CategoryManagement()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var viewModel = new CategoryListViewModel
            {
                Categories = categories.Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddCategory(CategoryViewModel model)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = errors });
                }

                // Check if category name already exists
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower());
                
                if (existingCategory != null)
                {
                    return Json(new { success = false, message = "A category with this name already exists" });
                }

                            var category = new Category
            {
                Name = model.Name.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Category added successfully",
                    category = new
                    {
                        id = category.Id,
                        name = category.Name,
                        isActive = category.IsActive,
                        createdAt = category.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        updatedAt = category.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error adding category: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCategory(CategoryViewModel model)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = errors });
                }

                var category = await _context.Categories.FindAsync(model.Id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found" });
                }

                // Check if category name already exists (excluding current category)
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower() && c.Id != model.Id);
                
                if (existingCategory != null)
                {
                    return Json(new { success = false, message = "A category with this name already exists" });
                }

                category.Name = model.Name.Trim();
                category.IsActive = model.IsActive;
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Category updated successfully",
                    category = new
                    {
                        id = category.Id,
                        name = category.Name,
                        isActive = category.IsActive,
                        createdAt = category.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        updatedAt = category.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error updating category: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found" });
                }

                // Check if category is being used by any kaizen forms
                var kaizenCount = await _context.KaizenForms
                    .CountAsync(k => k.Category == category.Name);
                
                if (kaizenCount > 0)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Cannot delete category. It is being used by {kaizenCount} kaizen form(s)." 
                    });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Category deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting category: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCategoryStatus(int id)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found" });
                }

                category.IsActive = !category.IsActive;
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = $"Category {(category.IsActive ? "activated" : "deactivated")} successfully",
                    isActive = category.IsActive
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error toggling category status: {ex.Message}" });
            }
        }

        // GET: /Admin/ExportAwardTrackingToExcel
        [HttpGet]
        public IActionResult ExportAwardTrackingToExcel(string startDate, string endDate, string department, string awardStatus)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
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

        // Gallery Management
        public IActionResult Gallery()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var galleries = _context.Gallery.Where(g => g != null && !string.IsNullOrEmpty(g.Title)).OrderBy(g => g.DisplayOrder).ThenBy(g => g.UploadDate).ToList();
            
            // Log for debugging
            Console.WriteLine($"Gallery Management: Found {galleries?.Count ?? 0} galleries");
            
            ViewBag.Galleries = galleries;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddGalleryImage(IFormFile image, string title, string description, int displayOrder = 0)
        {
            try
            {
                if (image == null || image.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select an image to upload.";
                    return RedirectToAction("Gallery");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["ErrorMessage"] = "Only JPG, PNG, GIF, and WebP images are allowed.";
                    return RedirectToAction("Gallery");
                }

                // Validate file size (max 10MB)
                if (image.Length > 10 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Image size must be less than 10MB.";
                    return RedirectToAction("Gallery");
                }

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "gallery");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Save the file
                                    using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // Save to database
                var gallery = new Gallery
                {
                    Title = title,
                    Description = description,
                    ImagePath = $"/uploads/gallery/{fileName}",
                    DisplayOrder = displayOrder,
                    UploadDate = DateTime.Now,
                    IsActive = true
                };

                _context.Gallery.Add(gallery);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Image uploaded successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while uploading the image.";
            }

            return RedirectToAction("Gallery");
        }



        [HttpPost]
        public async Task<IActionResult> UpdateGalleryImage(int id, string title, int displayOrder)
        {
            try
            {
                var gallery = await _context.Gallery.FindAsync(id);
                if (gallery == null)
                {
                    TempData["ErrorMessage"] = "Image not found.";
                    return RedirectToAction("Gallery");
                }

                gallery.Title = title;
                gallery.DisplayOrder = displayOrder;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Image updated successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating image: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating the image.";
            }

            return RedirectToAction("Gallery");
        }



        [HttpPost]
        public async Task<IActionResult> DeleteGalleryImage(int id)
        {
            try
            {
                var gallery = await _context.Gallery.FindAsync(id);
                if (gallery == null)
                {
                    TempData["ErrorMessage"] = "Image not found.";
                    return RedirectToAction("Gallery");
                }

                // Delete physical file
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", gallery.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.Gallery.Remove(gallery);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Image deleted successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting image: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while deleting the image.";
            }

            return RedirectToAction("Gallery");
        }



        [HttpPost]
        public async Task<IActionResult> UploadMultipleImages(IFormFileCollection images, string title, int displayOrder = 0)
        {
            try
            {
                if (images == null || images.Count == 0)
                {
                    TempData["ErrorMessage"] = "Please select images to upload.";
                    return RedirectToAction("Gallery");
                }

                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "gallery");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var uploadedCount = 0;

                foreach (var image in images)
                {
                    if (image.Length == 0) continue;

                    var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension) || image.Length > 10 * 1024 * 1024)
                        continue;

                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    var gallery = new Gallery
                    {
                        Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(image.FileName) : title,
                        ImagePath = $"/uploads/gallery/{fileName}",
                        DisplayOrder = displayOrder + uploadedCount,
                        UploadDate = DateTime.Now,
                        IsActive = true
                    };

                    _context.Gallery.Add(gallery);
                    uploadedCount++;
                }

                await _context.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"{uploadedCount} images uploaded successfully!" });
                }
                TempData["SuccessMessage"] = $"{uploadedCount} images uploaded successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading multiple images: {ex.Message}");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "An error occurred while uploading images." });
                }
                TempData["ErrorMessage"] = "An error occurred while uploading images.";
            }

            return RedirectToAction("Gallery");
        }

        // AWARD THRESHOLD MANAGEMENT METHODS

        [HttpGet]
        public async Task<IActionResult> AwardThresholds()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var thresholds = await _context.AwardThresholds
                .OrderBy(t => t.MinPercentage)
                .ToListAsync();

            return View(thresholds);
        }

        [HttpGet]
        public IActionResult AddAwardThreshold()
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            return View(new AwardThresholdViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> AddAwardThreshold(AwardThresholdViewModel model)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied." });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Validate percentage ranges
                if (model.MinPercentage >= model.MaxPercentage)
                {
                    return Json(new { success = false, message = "Minimum percentage must be less than maximum percentage." });
                }

                // Check for overlapping ranges
                var existingThresholds = await _context.AwardThresholds
                    .Where(t => t.IsActive)
                    .ToListAsync();

                foreach (var existing in existingThresholds)
                {
                    if ((model.MinPercentage < existing.MaxPercentage && model.MaxPercentage > existing.MinPercentage))
                    {
                        return Json(new { success = false, message = "This range overlaps with an existing award threshold." });
                    }
                }

                var threshold = new AwardThreshold
                {
                    AwardName = model.AwardName,
                    MinPercentage = model.MinPercentage,
                    MaxPercentage = model.MaxPercentage,
                    Description = null, // Description field removed
                    IsActive = model.IsActive,
                    CreatedBy = username,
                    CreatedAt = DateTime.Now
                };

                _context.AwardThresholds.Add(threshold);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Award threshold added successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding award threshold: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while adding the award threshold." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditAwardThreshold(int id)
        {
            // Check if user is admin
            var username = User.Identity?.Name;
            if (username?.ToLower() != "admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var threshold = await _context.AwardThresholds.FindAsync(id);
            if (threshold == null)
            {
                TempData["ErrorMessage"] = "Award threshold not found.";
                return RedirectToAction("AwardThresholds");
            }

            var model = new AwardThresholdViewModel
            {
                Id = threshold.Id,
                AwardName = threshold.AwardName,
                MinPercentage = threshold.MinPercentage,
                MaxPercentage = threshold.MaxPercentage,
                IsActive = threshold.IsActive
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditAwardThreshold(AwardThresholdViewModel model)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied." });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Validate percentage ranges
                if (model.MinPercentage >= model.MaxPercentage)
                {
                    return Json(new { success = false, message = "Minimum percentage must be less than maximum percentage." });
                }

                var threshold = await _context.AwardThresholds.FindAsync(model.Id);
                if (threshold == null)
                {
                    return Json(new { success = false, message = "Award threshold not found." });
                }

                // Check for overlapping ranges (excluding current threshold)
                var existingThresholds = await _context.AwardThresholds
                    .Where(t => t.IsActive && t.Id != model.Id)
                    .ToListAsync();

                foreach (var existing in existingThresholds)
                {
                    if ((model.MinPercentage < existing.MaxPercentage && model.MaxPercentage > existing.MinPercentage))
                    {
                        return Json(new { success = false, message = "This range overlaps with an existing award threshold." });
                    }
                }

                threshold.AwardName = model.AwardName;
                threshold.MinPercentage = model.MinPercentage;
                threshold.MaxPercentage = model.MaxPercentage;
                threshold.Description = null; // Description field removed
                threshold.IsActive = model.IsActive;
                threshold.UpdatedBy = username;
                threshold.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Award threshold updated successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating award threshold: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the award threshold." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAwardThreshold(int id)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied." });
                }

                var threshold = await _context.AwardThresholds.FindAsync(id);
                if (threshold == null)
                {
                    return Json(new { success = false, message = "Award threshold not found." });
                }

                _context.AwardThresholds.Remove(threshold);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Award threshold deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting award threshold: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the award threshold." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAwardThresholdStatus(int id)
        {
            try
            {
                // Check if user is admin
                var username = User.Identity?.Name;
                if (username?.ToLower() != "admin")
                {
                    return Json(new { success = false, message = "Access denied." });
                }

                var threshold = await _context.AwardThresholds.FindAsync(id);
                if (threshold == null)
                {
                    return Json(new { success = false, message = "Award threshold not found." });
                }

                threshold.IsActive = !threshold.IsActive;
                threshold.UpdatedBy = username;
                threshold.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var status = threshold.IsActive ? "activated" : "deactivated";
                return Json(new { success = true, message = $"Award threshold {status} successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling award threshold status: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the award threshold status." });
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
} 