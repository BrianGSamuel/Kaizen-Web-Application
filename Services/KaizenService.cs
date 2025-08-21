using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaizenWebApp.Services
{
    public class KaizenService : IKaizenService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<KaizenService> _logger;

        public KaizenService(AppDbContext context, ILogger<KaizenService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<KaizenForm>> GetAllKaizensAsync()
        {
            return await _context.KaizenForms
                .OrderByDescending(k => k.DateSubmitted)
                .ToListAsync();
        }

        public async Task<KaizenForm?> GetKaizenByIdAsync(int id)
        {
            return await _context.KaizenForms.FindAsync(id);
        }

        public async Task<KaizenForm> CreateKaizenAsync(KaizenFormViewModel model, string username)
        {
            var kaizen = new KaizenForm
            {
                KaizenNo = await GenerateKaizenNumberAsync(),
                DateSubmitted = DateTime.Now,
                Department = model.Department,
                EmployeeName = model.EmployeeName,
                EmployeeNo = model.EmployeeNo,
                SuggestionDescription = model.SuggestionDescription,
                CostSaving = model.CostSaving,
                CostSavingType = model.CostSavingType,
                DollarRate = model.DollarRate,
                OtherBenefits = model.OtherBenefits,
                Category = model.Category,
                Comments = model.Comments,
                CanImplementInOtherFields = model.CanImplementInOtherFields,
                ImplementationArea = model.ImplementationArea,
                EngineerStatus = "Pending",
                ManagerStatus = "Pending"
            };

            _context.KaizenForms.Add(kaizen);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Kaizen created by user {Username} with ID {KaizenId}", username, kaizen.Id);
            
            return kaizen;
        }

        public async Task<bool> UpdateKaizenAsync(int id, KaizenFormViewModel model)
        {
            var kaizen = await _context.KaizenForms.FindAsync(id);
            if (kaizen == null) return false;

            kaizen.Department = model.Department;
            kaizen.EmployeeName = model.EmployeeName;
            kaizen.EmployeeNo = model.EmployeeNo;
            kaizen.SuggestionDescription = model.SuggestionDescription;
            kaizen.CostSaving = model.CostSaving;
            kaizen.CostSavingType = model.CostSavingType;
            kaizen.DollarRate = model.DollarRate;
            kaizen.OtherBenefits = model.OtherBenefits;
            kaizen.Category = model.Category;
            kaizen.Comments = model.Comments;
            kaizen.CanImplementInOtherFields = model.CanImplementInOtherFields;
            kaizen.ImplementationArea = model.ImplementationArea;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteKaizenAsync(int id)
        {
            var kaizen = await _context.KaizenForms.FindAsync(id);
            if (kaizen == null) return false;

            _context.KaizenForms.Remove(kaizen);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<KaizenForm>> GetKaizensByUserAsync(string username)
        {
            return await _context.KaizenForms
                .Where(k => k.EmployeeName == username)
                .OrderByDescending(k => k.DateSubmitted)
                .ToListAsync();
        }

        public async Task<IEnumerable<KaizenForm>> GetPendingKaizensAsync()
        {
            return await _context.KaizenForms
                .Where(k => k.EngineerStatus == "Pending" || k.ManagerStatus == "Pending" ||
                           k.EngineerStatus == null || k.ManagerStatus == null)
                .OrderByDescending(k => k.DateSubmitted)
                .ToListAsync();
        }

        public async Task<IEnumerable<KaizenForm>> GetApprovedKaizensAsync()
        {
            return await _context.KaizenForms
                .Where(k => k.EngineerStatus == "Approved" && k.ManagerStatus == "Approved")
                .OrderByDescending(k => k.DateSubmitted)
                .ToListAsync();
        }

        public async Task<bool> ApproveKaizenAsync(int id, string approvedBy, string approvalType)
        {
            var kaizen = await _context.KaizenForms.FindAsync(id);
            if (kaizen == null) return false;

            if (approvalType.ToLower() == "engineer")
            {
                kaizen.EngineerStatus = "Approved";
                kaizen.EngineerApprovedBy = approvedBy;
            }
            else if (approvalType.ToLower() == "manager")
            {
                kaizen.ManagerStatus = "Approved";
                kaizen.ManagerApprovedBy = approvedBy;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectKaizenAsync(int id, string rejectedBy, string rejectionType)
        {
            var kaizen = await _context.KaizenForms.FindAsync(id);
            if (kaizen == null) return false;

            if (rejectionType.ToLower() == "engineer")
            {
                kaizen.EngineerStatus = "Rejected";
                kaizen.EngineerApprovedBy = rejectedBy;
            }
            else if (rejectionType.ToLower() == "manager")
            {
                kaizen.ManagerStatus = "Rejected";
                kaizen.ManagerApprovedBy = rejectedBy;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> GenerateKaizenNumberAsync()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            int count = await _context.KaizenForms
                .CountAsync(k => k.DateSubmitted.Date == DateTime.Today) + 1;
            return $"KZN-{datePart}-{count:D3}";
        }

        public async Task<decimal> GetTotalCostSavingsAsync()
        {
            return await _context.KaizenForms
                .Where(k => k.CostSaving.HasValue && k.EngineerStatus != "Rejected")
                .SumAsync(k => k.CostSaving.Value);
        }

        public async Task<int> GetTotalKaizensCountAsync()
        {
            return await _context.KaizenForms.CountAsync();
        }

        public async Task<IEnumerable<string>> GetDepartmentsAsync()
        {
            return await _context.KaizenForms
                .Where(k => !string.IsNullOrEmpty(k.Department))
                .Select(k => k.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
        }

        public async Task<IEnumerable<KaizenForm>> GetSimilarKaizensAsync(string suggestionDescription, string? costSavingType, string? otherBenefits, string department, int currentKaizenId)
        {
            try
            {
                _logger.LogInformation("Finding similar kaizens for department: {Department}, current kaizen ID: {CurrentKaizenId}", department, currentKaizenId);
                
                // Get all kaizens from the same department (excluding the current one)
                var similarKaizens = await _context.KaizenForms
                    .Where(k => k.Department == department && k.Id != currentKaizenId)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} total kaizens in department {Department}", similarKaizens.Count, department);

                var results = new List<KaizenForm>();
                var descriptionWords = suggestionDescription?.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                var benefitsWords = otherBenefits?.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

                foreach (var kaizen in similarKaizens)
                {
                    int similarityScore = 0;

                    // Check description similarity
                    if (!string.IsNullOrEmpty(kaizen.SuggestionDescription))
                    {
                        var kaizenDescriptionWords = kaizen.SuggestionDescription.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var commonDescriptionWords = descriptionWords.Intersect(kaizenDescriptionWords, StringComparer.OrdinalIgnoreCase).Count();
                        similarityScore += commonDescriptionWords * 2; // Description similarity has higher weight
                    }

                    // Check benefits similarity
                    if (!string.IsNullOrEmpty(kaizen.OtherBenefits) && !string.IsNullOrEmpty(otherBenefits))
                    {
                        var kaizenBenefitsWords = kaizen.OtherBenefits.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var commonBenefitsWords = benefitsWords.Intersect(kaizenBenefitsWords, StringComparer.OrdinalIgnoreCase).Count();
                        similarityScore += commonBenefitsWords;
                    }

                    // Check cost saving type similarity
                    if (!string.IsNullOrEmpty(kaizen.CostSavingType) && !string.IsNullOrEmpty(costSavingType))
                    {
                        if (kaizen.CostSavingType.Equals(costSavingType, StringComparison.OrdinalIgnoreCase))
                        {
                            similarityScore += 3; // Cost saving type match has high weight
                        }
                    }

                    // Check if both have cost savings
                    if (kaizen.CostSaving.HasValue && kaizen.CostSaving > 0)
                    {
                        similarityScore += 1;
                    }

                    // Only include kaizens with a minimum similarity score
                    if (similarityScore >= 2)
                    {
                        results.Add(kaizen);
                    }
                }

                // Return top 5 most similar kaizens, ordered by similarity score
                var finalResults = results
                    .OrderByDescending(k => {
                        var kDescWords = k.SuggestionDescription?.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                        var kBenefitsWords = k.OtherBenefits?.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                        
                        int score = 0;
                        var commonDescWords = descriptionWords.Intersect(kDescWords, StringComparer.OrdinalIgnoreCase).Count();
                        score += commonDescWords * 2;
                        
                        var commonBenefitsWords = benefitsWords.Intersect(kBenefitsWords, StringComparer.OrdinalIgnoreCase).Count();
                        score += commonBenefitsWords;
                        
                        if (!string.IsNullOrEmpty(k.CostSavingType) && !string.IsNullOrEmpty(costSavingType))
                        {
                            if (k.CostSavingType.Equals(costSavingType, StringComparison.OrdinalIgnoreCase))
                            {
                                score += 3;
                            }
                        }
                        
                        if (k.CostSaving.HasValue && k.CostSaving > 0)
                        {
                            score += 1;
                        }
                        
                        return score;
                    })
                    .Take(5)
                    .ToList();

                _logger.LogInformation("Returning {Count} similar kaizens for department {Department}", finalResults.Count, department);
                return finalResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar kaizens for department {Department}", department);
                return new List<KaizenForm>();
            }
        }
    }
}
