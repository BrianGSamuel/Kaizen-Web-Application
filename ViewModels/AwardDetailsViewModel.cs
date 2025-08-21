using KaizenWebApp.Models;

namespace KaizenWebApp.ViewModels
{
    public class AwardDetailsViewModel
    {
        public KaizenForm Kaizen { get; set; } = new KaizenForm();
        public List<MarkingCriteria> MarkingCriteria { get; set; } = new List<MarkingCriteria>();
        public List<KaizenMarkingScore> ExistingScores { get; set; } = new List<KaizenMarkingScore>();
    }
}
