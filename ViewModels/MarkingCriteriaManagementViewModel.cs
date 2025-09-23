using KaizenWebApp.Models;

namespace KaizenWebApp.ViewModels
{
    public class MarkingCriteriaManagementViewModel
    {
        public List<MarkingCriteria> MarkingCriteria { get; set; } = new List<MarkingCriteria>();
        public List<AwardThreshold> AwardThresholds { get; set; } = new List<AwardThreshold>();
    }
}
