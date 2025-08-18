using System;

namespace KaizenWebApp.ViewModels
{
    public class UserDashboardViewModel
    {
        // User's Personal Statistics
        public int TotalKaizensSubmitted { get; set; }
        public int CurrentMonthSubmissions { get; set; }
        public int PreviousMonthSubmissions { get; set; }

        // Department Target Information
        public int DepartmentTarget { get; set; }
        public int DepartmentCurrentMonthSubmissions { get; set; }
        public double DepartmentTargetAchievement { get; set; }

        // User's Kaizen Status Statistics
        public int PendingKaizens { get; set; }
        public int ApprovedKaizens { get; set; }
        public int RejectedKaizens { get; set; }
        public int TotalReviewed { get; set; }

        // Cost Savings
        public decimal TotalCostSavings { get; set; }
        public decimal CurrentMonthCostSavings { get; set; }

        // User Information
        public string EmployeeName { get; set; }
        public string EmployeeNumber { get; set; }
        public string Department { get; set; }

        // Current Period Information
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        // Additional properties for enhanced dashboard
        public int FullyImplementedKaizens { get; set; }
        public int BothApprovedKaizens { get; set; }
        public int PreviousMonthTarget { get; set; }
        public double PreviousMonthAchievement { get; set; }

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
                if (DepartmentTargetAchievement >= 100) return "Completed";
                if (DepartmentTargetAchievement >= 80) return "On Track";
                if (DepartmentTargetAchievement >= 50) return "Behind";
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

        public double ApprovalRate
        {
            get
            {
                if (TotalReviewed == 0) return 0;
                return ((double)ApprovedKaizens / TotalReviewed) * 100;
            }
        }

        public double RejectionRate
        {
            get
            {
                if (TotalReviewed == 0) return 0;
                return ((double)RejectedKaizens / TotalReviewed) * 100;
            }
        }

        public double UserContributionToDepartment
        {
            get
            {
                if (DepartmentCurrentMonthSubmissions == 0) return 0;
                return ((double)CurrentMonthSubmissions / DepartmentCurrentMonthSubmissions) * 100;
            }
        }
    }
}







