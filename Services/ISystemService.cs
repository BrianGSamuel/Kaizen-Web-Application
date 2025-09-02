using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;

namespace KaizenWebApp.Services
{
    public interface ISystemService
    {
        Task<SystemMaintenance> GetSystemMaintenanceStatusAsync();
        Task<bool> SetSystemMaintenanceStatusAsync(bool isOffline, string? message, string updatedBy);
        Task<bool> IsSystemOfflineAsync();
        Task<List<Notification>> GetNotificationsForUserAsync(string username, string role);
        Task<bool> SendNotificationAsync(SettingsViewModel model, string createdBy);
        Task<bool> MarkNotificationAsReadAsync(int notificationId, string username);
        Task<int> GetUnreadNotificationCountAsync(string username, string role);
        Task<bool> DeleteLastNotificationAsync();
        Task<bool> DeleteNotificationAsync(int notificationId, string username);
    }
}
