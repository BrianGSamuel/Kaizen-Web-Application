using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaizenWebApp.Models
{
    public class KaizenForm
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string KaizenNo { get; set; }

        [Required]
        public DateTime DateSubmitted { get; set; }

        public DateTime? DateImplemented { get; set; } // Made nullable

        [StringLength(100)]
        public string? Department { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; }

        [Required]
        [StringLength(20)]
        public string EmployeeNo { get; set; }

        [Required]
        [StringLength(1000)]
        public string SuggestionDescription { get; set; }

        public decimal? CostSaving { get; set; }

        [StringLength(50)]
        public string? CostSavingType { get; set; } // "NoCostSaving" or "HasCostSaving"

        public decimal? DollarRate { get; set; }

        [StringLength(1000)]
        public string? OtherBenefits { get; set; }

        [StringLength(255)]
        public string? BeforeKaizenImagePath { get; set; }

        [StringLength(255)]
        public string? AfterKaizenImagePath { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Default status: Pending, Approved, Rejected

        [StringLength(500)]
        public string? Category { get; set; } // Comma-separated list of categories

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        // New fields for simplified executive/engineer form
        [StringLength(10)]
        public string? CanImplementInOtherFields { get; set; } // "Yes" or "No"

        [StringLength(500)]
        public string? ImplementationArea { get; set; }
    }
}