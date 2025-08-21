using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaizenWebApp.Models
{
    public class MarkingCriteria
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string CriteriaName { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        public int MaxScore { get; set; }

        [Required]
        public int Weight { get; set; } // Weight percentage (1-100)

        [StringLength(50)]
        public string Category { get; set; } // e.g., "Innovation", "Cost Saving", "Implementation", etc.

        [Required]
        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
    }
}
