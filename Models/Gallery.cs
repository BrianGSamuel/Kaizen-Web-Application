using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class Gallery
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(500)]
        public string ImagePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.Now;

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}


