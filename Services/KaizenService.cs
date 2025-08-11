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
    }
}
