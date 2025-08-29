using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class FAQ
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Answer { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Category { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }
    }
}


