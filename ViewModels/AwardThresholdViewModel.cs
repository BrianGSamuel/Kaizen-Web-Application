using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.ViewModels
{
    public class AwardThresholdViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Award name is required")]
        [StringLength(50, ErrorMessage = "Award name cannot exceed 50 characters")]
        [Display(Name = "Award Name")]
        public string AwardName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Minimum percentage is required")]
        [Range(0, 100, ErrorMessage = "Minimum percentage must be between 0 and 100")]
        [Display(Name = "Minimum Percentage")]
        public decimal MinPercentage { get; set; }

        [Required(ErrorMessage = "Maximum percentage is required")]
        [Range(0, 100, ErrorMessage = "Maximum percentage must be between 0 and 100")]
        [Display(Name = "Maximum Percentage")]
        public decimal MaxPercentage { get; set; }


        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
