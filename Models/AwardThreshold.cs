using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaizenWebApp.Models
{
    public class AwardThreshold
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string AwardName { get; set; } // e.g., "1st Place", "2nd Place", "3rd Place"

        [Required]
        public decimal MinPercentage { get; set; } // Minimum percentage required

        [Required]
        public decimal MaxPercentage { get; set; } // Maximum percentage (100 for 1st place)

        [StringLength(200)]
        public string? Description { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
    }
}
