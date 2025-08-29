using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.ViewModels
{
    public class CategoryViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class CategoryListViewModel
    {
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
        public CategoryViewModel NewCategory { get; set; } = new CategoryViewModel();
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
