using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaizenWebApp.Services
{
    public class SystemService : ISystemService
    {
        private readonly AppDbContext _context;

        public SystemService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SystemMaintenance> GetSystemMaintenanceStatusAsync()
        {
            var maintenance = await _context.SystemMaintenance.FirstOrDefaultAsync();
            
            if (maintenance == null)
            {
                // Create default maintenance record if none exists
                maintenance = new SystemMaintenance
                {
                    IsSystemOffline = false,
                    MaintenanceMessage = null,
                    LastUpdated = DateTime.Now,
                    UpdatedBy = "System"
                };
                _context.SystemMaintenance.Add(maintenance);
                await _context.SaveChangesAsync();
            }
            
            return maintenance;
        }

        public async Task<bool> SetSystemMaintenanceStatusAsync(bool isOffline, string? message, string updatedBy)
        {
            try
            {
                var maintenance = await GetSystemMaintenanceStatusAsync();
                maintenance.IsSystemOffline = isOffline;
                maintenance.MaintenanceMessage = message;
                maintenance.LastUpdated = DateTime.Now;
                maintenance.UpdatedBy = updatedBy;
                
                if (isOffline)
                {
                    maintenance.MaintenanceStartTime = DateTime.Now;
                }
                else
                {
                    maintenance.MaintenanceEndTime = DateTime.Now;
                }
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsSystemOfflineAsync()
        {
            var maintenance = await GetSystemMaintenanceStatusAsync();
            return maintenance.IsSystemOffline;
        }

        public async Task<List<Notification>> GetNotificationsForUserAsync(string username, string role)
        {
            var notifications = await _context.Notifications
                .Where(n => 
                    (n.RecipientRole == "All") ||
                    (n.RecipientRole == role) ||
                    (n.RecipientUsername == username))
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
                
            return notifications;
        }

        public async Task<bool> SendNotificationAsync(SettingsViewModel model, string createdBy)
        {
            try
            {
                var notification = new Notification
                {
                    Title = model.NotificationTitle,
                    Message = model.NotificationMessage,
                    RecipientRole = model.RecipientRole,
                    RecipientUsername = model.RecipientUsername,
                    NotificationType = model.NotificationType,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.Now
                };
                
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> MarkNotificationAsReadAsync(int notificationId, string username)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> GetUnreadNotificationCountAsync(string username, string role)
        {
            return await _context.Notifications
                .CountAsync(n => 
                    !n.IsRead &&
                    ((n.RecipientRole == "All") ||
                     (n.RecipientRole == role) ||
                     (n.RecipientUsername == username)));
        }

        public async Task<bool> DeleteLastNotificationAsync()
        {
            try
            {
                var lastNotification = await _context.Notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefaultAsync();
                
                if (lastNotification != null)
                {
                    _context.Notifications.Remove(lastNotification);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
