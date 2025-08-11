using System;

namespace KaizenWebApp.ViewModels
{
    public class ManagerDashboardViewModel
    {
        // Current Month Statistics
        public int CurrentMonthSubmissions { get; set; }
        public int CurrentMonthTarget { get; set; }
        public double CurrentMonthAchievement { get; set; }
        public decimal CurrentMonthCostSavings { get; set; }

        // Previous Month Statistics
        public int PreviousMonthSubmissions { get; set; }
        public int PreviousMonthTarget { get; set; }
        public double PreviousMonthAchievement { get; set; }
        public decimal PreviousMonthCostSavings { get; set; }

        // Approval Statistics
        public int PendingApprovals { get; set; }
        public int CompletedKaizens { get; set; }

        // Department Information
        public string Department { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        // Helper properties for display
        public string CurrentMonthName => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(CurrentMonth);
        public string PreviousMonthName
        {
            get
            {
                var previousMonth = CurrentMonth == 1 ? 12 : CurrentMonth - 1;
                return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(previousMonth);
            }
        }

        public string AchievementStatus
        {
            get
            {
                if (CurrentMonthAchievement >= 100) return "Completed";
                if (CurrentMonthAchievement >= 80) return "On Track";
                if (CurrentMonthAchievement >= 50) return "Behind";
                return "At Risk";
            }
        }

        public string AchievementStatusClass
        {
            get
            {
                return AchievementStatus.ToLower().Replace(" ", "-");
            }
        }

        public double MonthOverMonthChange
        {
            get
            {
                if (PreviousMonthSubmissions == 0) return 0;
                return ((double)(CurrentMonthSubmissions - PreviousMonthSubmissions) / PreviousMonthSubmissions) * 100;
            }
        }

        public double CostSavingsChange
        {
            get
            {
                if (PreviousMonthCostSavings == 0) return 0;
                return ((double)(CurrentMonthCostSavings - PreviousMonthCostSavings) / (double)PreviousMonthCostSavings) * 100;
            }
        }
    }
}
