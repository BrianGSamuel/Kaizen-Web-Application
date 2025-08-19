using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class SystemMaintenance
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public bool IsSystemOffline { get; set; } = false;
        
        [StringLength(500)]
        public string? MaintenanceMessage { get; set; }
        
        public DateTime? MaintenanceStartTime { get; set; }
        
        public DateTime? MaintenanceEndTime { get; set; }
        
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        
        public string? UpdatedBy { get; set; }
    }
}


