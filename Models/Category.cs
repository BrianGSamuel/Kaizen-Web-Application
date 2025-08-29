using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property for related kaizen forms
        public virtual ICollection<KaizenForm> KaizenForms { get; set; } = new List<KaizenForm>();
    }
}
