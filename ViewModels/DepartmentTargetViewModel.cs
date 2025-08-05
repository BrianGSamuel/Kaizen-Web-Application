using System;
using System.Collections.Generic;

namespace KaizenWebApp.ViewModels
{
    public class DepartmentTargetViewModel
    {
        public string Department { get; set; }
        public int TargetCount { get; set; }
        public int AchievedCount { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public double AchievementPercentage => TargetCount > 0 ? (double)AchievedCount / TargetCount * 100 : 0;
        public string Status => AchievementPercentage >= 100 ? "Completed" : 
                               AchievementPercentage >= 80 ? "On Track" : 
                               AchievementPercentage >= 50 ? "Behind" : "At Risk";
    }

    public class DepartmentTargetsPageViewModel
    {
        public List<DepartmentTargetViewModel> DepartmentTargets { get; set; } = new List<DepartmentTargetViewModel>();
        public int SelectedYear { get; set; } = DateTime.Now.Year;
        public int SelectedMonth { get; set; } = DateTime.Now.Month;
        public List<int> AvailableYears { get; set; } = new List<int>();
        public List<int> AvailableMonths { get; set; } = new List<int>();
        public int TotalTarget { get; set; }
        public int TotalAchieved { get; set; }
        public double OverallAchievementPercentage => TotalTarget > 0 ? (double)TotalAchieved / TotalTarget * 100 : 0;
    }
} 