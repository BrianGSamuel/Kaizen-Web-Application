using System.Threading.Tasks;

namespace KaizenWebApp.Services
{
    public interface IEmailService
    {
        Task<bool> SendKaizenNotificationAsync(string toEmail, string kaizenNo, string employeeName, string department, string suggestionDescription, string websiteUrl);
        Task<bool> SendManagerNotificationAsync(string toEmail, string kaizenNo, string employeeName, string department, string engineerName, string engineerComments, string websiteUrl);
        Task<bool> SendInterDepartmentNotificationAsync(string toEmail, string kaizenNo, string employeeName, string sourceDepartment, string targetDepartment, string suggestionDescription, string websiteUrl);
    }
}

