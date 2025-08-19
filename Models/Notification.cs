using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;
        
        [Required]
        public string RecipientRole { get; set; } = string.Empty; // "All", "User", "Supervisor", "Manager", "Engineer"
        
        public string? RecipientUsername { get; set; } // For specific user notifications
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? ReadAt { get; set; }
        
        public string CreatedBy { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? NotificationType { get; set; } // "info", "warning", "success", "error"
    }
}


