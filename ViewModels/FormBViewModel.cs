using System;

namespace KaizenWebApp.ViewModels
{
    public class FormBViewModel
    {
        public int Id { get; set; }
        public string KaizenNo { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeNo { get; set; }
        public string Department { get; set; }
        public string SuggestionDescription { get; set; }
        public string BeforeKaizenImagePath { get; set; }
        public string AfterKaizenImagePath { get; set; }
        public string EmployeePhotoPath { get; set; }
        public string OtherBenefits { get; set; }
        public DateTime ImplementationDate { get; set; }
        public decimal? ImplementationCost { get; set; }
        public string ImplementationDetails { get; set; }
        public string Results { get; set; }
        public string Remarks { get; set; }
        public string ManagerComments { get; set; }
        public string ManagerSignature { get; set; }
        public DateTime DateSubmitted { get; set; }
    }
} 