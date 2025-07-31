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
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [NotMapped] // Not stored in DB, used only for confirmation during registration
        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
