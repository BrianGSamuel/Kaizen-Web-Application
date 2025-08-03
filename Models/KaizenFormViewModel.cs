using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class KaizenFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string KaizenNo { get; set; }

        [Required]
        [Display(Name = "Date Submitted")]
        public DateTime DateSubmitted { get; set; }

        [Display(Name = "Date Implemented")]
        public DateTime? DateImplemented { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Employee Number")]
        public string EmployeeNo { get; set; }

        [Display(Name = "Employee Photo")]
        public IFormFile? EmployeePhoto { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Suggestion Description")]
        public string SuggestionDescription { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Cost Saving ($ per year)")]
        public decimal? CostSaving { get; set; }

        [Display(Name = "Cost Saving Type")]
        public string? CostSavingType { get; set; } // "NoCostSaving" or "HasCostSaving"

        [Display(Name = "Current Dollar Rate")]
        public decimal? DollarRate { get; set; }

        [StringLength(1000)]
        [Display(Name = "Other Benefits")]
        public string? OtherBenefits { get; set; }

        [Display(Name = "Before Kaizen Image")]
        public IFormFile? BeforeKaizenImage { get; set; }

        [Display(Name = "After Kaizen Image")]
        public IFormFile? AfterKaizenImage { get; set; }

        // Properties for existing image paths
        public string? BeforeKaizenImagePath { get; set; }
        public string? AfterKaizenImagePath { get; set; }
        public string? EmployeePhotoPath { get; set; }

        [StringLength(500)]
        [Display(Name = "Category")]
        public string? Category { get; set; } // Comma-separated list of categories

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [StringLength(1000)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        // New fields for simplified executive/engineer form
        [StringLength(10)]
        [Display(Name = "Can Implement In Other Fields")]
        public string? CanImplementInOtherFields { get; set; } // "Yes" or "No"

        [StringLength(500)]
        [Display(Name = "Implementation Area")]
        public string? ImplementationArea { get; set; }

        // Manager comment fields
        [StringLength(1000)]
        [Display(Name = "Manager Comments")]
        public string? ManagerComments { get; set; }

        [StringLength(100)]
        [Display(Name = "Manager Signature")]
        public string? ManagerSignature { get; set; }
    }
}