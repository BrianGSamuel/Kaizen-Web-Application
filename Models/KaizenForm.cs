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

        [StringLength(10)]
        public string? Plant { get; set; } // Added Plant field for new Kaizen number format

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; }

        [Required]
        [StringLength(20)]
        public string EmployeeNo { get; set; }

        [StringLength(255)]
        public string? EmployeePhotoPath { get; set; }

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

        [StringLength(500)]
        public string? Category { get; set; } // Comma-separated list of categories

        [StringLength(1000)]
        public string? Comments { get; set; }

        // New fields for simplified executive/engineer form
        [StringLength(10)]
        public string? CanImplementInOtherFields { get; set; } // "Yes" or "No"

        [StringLength(500)]
        public string? ImplementationArea { get; set; }

        // New fields for manager comments
        [StringLength(1000)]
        public string? ManagerComments { get; set; }

        [StringLength(100)]
        public string? ManagerSignature { get; set; }

        // New fields for separate engineer and manager approval tracking
        [StringLength(20)]
        public string? EngineerStatus { get; set; } // "Pending", "Approved", "Rejected"

        [StringLength(100)]
        public string? EngineerApprovedBy { get; set; }

        [StringLength(20)]
        public string? ManagerStatus { get; set; } // "Pending", "Approved", "Rejected"

        [StringLength(100)]
        public string? ManagerApprovedBy { get; set; }

        // Award tracking fields
        [StringLength(20)]
        public string? AwardPrice { get; set; } // "1ST PRICE", "2ND PRICE", "3RD PRICE", "NO PRICE"

        [StringLength(1000)]
        public string? CommitteeComments { get; set; }

        [StringLength(100)]
        public string? CommitteeSignature { get; set; }

        public DateTime? AwardDate { get; set; }
    }
}