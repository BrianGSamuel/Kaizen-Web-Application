using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.ViewModels
{
    public class SettingsViewModel
    {
        // System Maintenance
        public bool IsSystemOffline { get; set; }
        
        [StringLength(500)]
        public string? MaintenanceMessage { get; set; }
        
        public DateTime? MaintenanceStartTime { get; set; }
        
        public DateTime? MaintenanceEndTime { get; set; }
        
        // Notification
        [Required(ErrorMessage = "Notification title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string NotificationTitle { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Notification message is required")]
        [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
        public string NotificationMessage { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Please select recipient role")]
        public string RecipientRole { get; set; } = "All";
        
        public string? RecipientUsername { get; set; }
        
        [Required(ErrorMessage = "Please select notification type")]
        public string NotificationType { get; set; } = "info";
        
        // Available options
        public List<string> AvailableRoles { get; set; } = new List<string> 
        { 
            "All", "User", "Supervisor", "Manager", "Engineer" 
        };
        
        public List<string> NotificationTypes { get; set; } = new List<string> 
        { 
            "info", "warning", "success", "error" 
        };
        
        public List<Users> AvailableUsers { get; set; } = new List<Users>();
    }
}


