using KaizenWebApp.Models;

namespace KaizenWebApp.ViewModels
{
    public class AwardDetailsViewModel
    {
        public KaizenForm Kaizen { get; set; } = new KaizenForm();
        public List<MarkingCriteria> MarkingCriteria { get; set; } = new List<MarkingCriteria>();
        public List<KaizenMarkingScore> ExistingScores { get; set; } = new List<KaizenMarkingScore>();
        public int TotalScore { get; set; }
        public int TotalWeight { get; set; }
        public double Percentage { get; set; }
        public string AwardName { get; set; } = string.Empty;
        public string AwardClass { get; set; } = string.Empty;
    }
}
