using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace KaizenWebApp.ViewModels
{
    public class VerifyUsernameViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [XmlText]
        public string Username { get; set; }
    }
}
