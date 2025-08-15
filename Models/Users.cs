using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaizenWebApp.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string DepartmentName { get; set; }

        [Required]
        public string Plant { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; } = "User"; // Default role

        [StringLength(100)]
        [Display(Name = "Employee Name")]
        public string? EmployeeName { get; set; }

        [StringLength(20)]
        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        public string? EmployeePhotoPath { get; set; }

        [NotMapped] // Not stored in DB, used only for confirmation during registration
        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
