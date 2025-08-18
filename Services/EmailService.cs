using System;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using System.Text;

namespace KaizenWebApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _senderEmail = "brianipad2003@gmail.com";
        private readonly string _senderPassword = "ckix qgie cbqa gjam";
        private readonly string _senderName = "Kaizen Web Application";

        public async Task<bool> SendKaizenNotificationAsync(string toEmail, string kaizenNo, string employeeName, string department, string suggestionDescription, string websiteUrl)
        {
            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_senderEmail, _senderName),
                        Subject = $"New Kaizen Suggestion Submitted - {kaizenNo}",
                        Body = GenerateEmailBody(kaizenNo, employeeName, department, suggestionDescription, websiteUrl),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log the error (you might want to use a proper logging framework)
                Console.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
        }

        private string GenerateEmailBody(string kaizenNo, string employeeName, string department, string suggestionDescription, string websiteUrl)
        {
            var body = new StringBuilder();
            body.AppendLine("<html><body>");
            body.AppendLine("<h2>New Kaizen Suggestion Submitted</h2>");
            body.AppendLine("<p>A new kaizen suggestion has been submitted and requires your attention.</p>");
            body.AppendLine("<br/>");
            body.AppendLine("<h3>Suggestion Details:</h3>");
            body.AppendLine("<table style='border-collapse: collapse; width: 100%;'>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Kaizen Number:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{kaizenNo}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Employee Name:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{employeeName}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Department:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{department}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Suggestion Description:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{suggestionDescription}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("</table>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Please review this suggestion by clicking the link below:</p>");
            body.AppendLine($"<p><a href='{websiteUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>View Kaizen Suggestion</a></p>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Thank you for your attention to this matter.</p>");
            body.AppendLine("<p>Best regards,<br/>Kaizen Web Application Team</p>");
            body.AppendLine("</body></html>");

            return body.ToString();
        }

        public async Task<bool> SendManagerNotificationAsync(string toEmail, string kaizenNo, string employeeName, string department, string engineerName, string engineerComments, string websiteUrl)
        {
            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_senderEmail, _senderName),
                        Subject = $"Engineer Review Completed - {kaizenNo}",
                        Body = GenerateManagerEmailBody(kaizenNo, employeeName, department, engineerName, engineerComments, websiteUrl),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending manager email: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendInterDepartmentNotificationAsync(string toEmail, string kaizenNo, string employeeName, string sourceDepartment, string targetDepartment, string suggestionDescription, string websiteUrl)
        {
            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_senderEmail, _senderName),
                        Subject = $"Inter-Department Kaizen Suggestion - {kaizenNo}",
                        Body = GenerateInterDepartmentEmailBody(kaizenNo, employeeName, sourceDepartment, targetDepartment, suggestionDescription, websiteUrl),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending inter-department email: {ex.Message}");
                return false;
            }
        }

        private string GenerateInterDepartmentEmailBody(string kaizenNo, string employeeName, string sourceDepartment, string targetDepartment, string suggestionDescription, string websiteUrl)
        {
            var body = new StringBuilder();
            body.AppendLine("<html><body>");
            body.AppendLine("<h2>Inter-Department Kaizen Suggestion</h2>");
            body.AppendLine("<p>A kaizen suggestion has been identified as potentially implementable in your department.</p>");
            body.AppendLine("<br/>");
            body.AppendLine("<h3>Suggestion Details:</h3>");
            body.AppendLine("<table style='border-collapse: collapse; width: 100%;'>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Kaizen Number:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{kaizenNo}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Employee Name:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{employeeName}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Source Department:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{sourceDepartment}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Target Department:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{targetDepartment}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Suggestion Description:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{suggestionDescription}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("</table>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>This suggestion has been identified as potentially beneficial for implementation in your department. Please review and consider if this improvement can be applied to your area.</p>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Please review this suggestion by clicking the link below:</p>");
            body.AppendLine($"<p><a href='{websiteUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Review Kaizen Suggestion</a></p>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Thank you for your attention to this matter.</p>");
            body.AppendLine("<p>Best regards,<br/>Kaizen Web Application Team</p>");
            body.AppendLine("</body></html>");

            return body.ToString();
        }

        private string GenerateManagerEmailBody(string kaizenNo, string employeeName, string department, string engineerName, string engineerComments, string websiteUrl)
        {
            var body = new StringBuilder();
            body.AppendLine("<html><body>");
            body.AppendLine("<h2>Engineer Review Completed</h2>");
            body.AppendLine("<p>An engineer has completed their review of a kaizen suggestion and it now requires your approval.</p>");
            body.AppendLine("<br/>");
            body.AppendLine("<h3>Kaizen Details:</h3>");
            body.AppendLine("<table style='border-collapse: collapse; width: 100%;'>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Kaizen Number:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{kaizenNo}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Employee Name:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{employeeName}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Department:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{department}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Engineer:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{engineerName}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("<tr style='background-color: #f2f2f2;'>");
            body.AppendLine("<td style='border: 1px solid #ddd; padding: 8px; font-weight: bold;'>Engineer Comments:</td>");
            body.AppendLine($"<td style='border: 1px solid #ddd; padding: 8px;'>{engineerComments ?? "No comments provided"}</td>");
            body.AppendLine("</tr>");
            body.AppendLine("</table>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Please review and approve this kaizen suggestion by clicking the link below:</p>");
            body.AppendLine($"<p><a href='{websiteUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Review Kaizen Suggestion</a></p>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Thank you for your attention to this matter.</p>");
            body.AppendLine("<p>Best regards,<br/>Kaizen Web Application Team</p>");
            body.AppendLine("</body></html>");

            return body.ToString();
        }
    }
}
