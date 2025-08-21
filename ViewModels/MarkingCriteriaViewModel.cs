using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.ViewModels
{
    public class MarkingCriteriaViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Criteria name is required")]
        [StringLength(200, ErrorMessage = "Criteria name cannot exceed 200 characters")]
        [Display(Name = "Criteria Name")]
        public string CriteriaName { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Maximum score is required")]
        [Range(1, 100, ErrorMessage = "Maximum score must be between 1 and 100")]
        [Display(Name = "Maximum Score")]
        public int MaxScore { get; set; }

        [Required(ErrorMessage = "Weight is required")]
        [Range(1, 100, ErrorMessage = "Weight must be between 1 and 100")]
        [Display(Name = "Weight (%)")]
        public int Weight { get; set; }

        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        [Display(Name = "Notes")]
        public string Notes { get; set; }
    }
}
