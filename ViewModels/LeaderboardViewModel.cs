using System;

namespace KaizenWebApp.ViewModels
{
    public class LeaderboardViewModel
    {
        public int Rank { get; set; }
        public string? EmployeeNo { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeePhotoPath { get; set; }
        public string? Department { get; set; }
        public int TotalKaizens { get; set; }
        public int ApprovedKaizens { get; set; }
        public int PendingKaizens { get; set; }
        public int RejectedKaizens { get; set; }
        public decimal TotalCostSaving { get; set; }
        public DateTime LastSubmission { get; set; }
    }
}
