using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;

namespace KaizenWebApp.Services
{
    public interface IKaizenService
    {
        Task<IEnumerable<KaizenForm>> GetAllKaizensAsync();
        Task<KaizenForm?> GetKaizenByIdAsync(int id);
        Task<KaizenForm> CreateKaizenAsync(KaizenFormViewModel model, string username);
        Task<bool> UpdateKaizenAsync(int id, KaizenFormViewModel model);
        Task<bool> DeleteKaizenAsync(int id);
        Task<IEnumerable<KaizenForm>> GetKaizensByUserAsync(string username);
        Task<IEnumerable<KaizenForm>> GetPendingKaizensAsync();
        Task<IEnumerable<KaizenForm>> GetApprovedKaizensAsync();
        Task<bool> ApproveKaizenAsync(int id, string approvedBy, string approvalType);
        Task<bool> RejectKaizenAsync(int id, string rejectedBy, string rejectionType);
        Task<string> GenerateKaizenNumberAsync();
        Task<decimal> GetTotalCostSavingsAsync();
        Task<int> GetTotalKaizensCountAsync();
        Task<IEnumerable<string>> GetDepartmentsAsync();
        Task<IEnumerable<KaizenForm>> GetSimilarKaizensAsync(string suggestionDescription, string? costSavingType, string? otherBenefits, string department, int currentKaizenId);
    }
}
