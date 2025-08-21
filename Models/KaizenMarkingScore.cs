using System.ComponentModel.DataAnnotations;

namespace KaizenWebApp.Models
{
    public class KaizenMarkingScore
    {
        public int Id { get; set; }
        
        [Required]
        public int KaizenId { get; set; }
        
        [Required]
        public int MarkingCriteriaId { get; set; }
        
        [Required]
        [Range(0, 100)]
        public int Score { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public string CreatedBy { get; set; } = string.Empty;
        
        // Navigation properties
        public KaizenForm Kaizen { get; set; } = new KaizenForm();
        public MarkingCriteria MarkingCriteria { get; set; } = new MarkingCriteria();
    }
}
