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

        public async Task<bool> SendKaizenNotificationWithSimilarSuggestionsAsync(string toEmail, string kaizenNo, string employeeName, string department, string suggestionDescription, string websiteUrl, IEnumerable<KaizenWebApp.Models.KaizenForm> similarKaizens)
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
                        Subject = $"New Kaizen Suggestion Submitted - {kaizenNo} (with Similar Suggestions)",
                        Body = GenerateEmailBodyWithSimilarSuggestions(kaizenNo, employeeName, department, suggestionDescription, websiteUrl, similarKaizens),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email with similar suggestions: {ex.Message}");
                return false;
            }
        }

        private string GenerateEmailBodyWithSimilarSuggestions(string kaizenNo, string employeeName, string department, string suggestionDescription, string websiteUrl, IEnumerable<KaizenWebApp.Models.KaizenForm> similarKaizens)
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

            // Add similar suggestions section if any found
            if (similarKaizens.Any())
            {
                body.AppendLine("<h3>📋 Similar Suggestions from Your Department:</h3>");
                body.AppendLine("<p><em>We found the following similar kaizen suggestions that might be relevant for your review:</em></p>");
                body.AppendLine("<br/>");

                foreach (var similarKaizen in similarKaizens)
                {
                    body.AppendLine("<div style='border: 1px solid #ddd; border-radius: 5px; padding: 15px; margin-bottom: 15px; background-color: #f9f9f9;'>");
                    body.AppendLine($"<h4 style='margin-top: 0; color: #2c3e50;'>📝 {similarKaizen.KaizenNo}</h4>");
                    body.AppendLine("<table style='border-collapse: collapse; width: 100%; font-size: 14px;'>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold; width: 30%;'>Submitted By:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.EmployeeName}</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Date:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.DateSubmitted:MMMM dd, yyyy}</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Description:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.SuggestionDescription}</td>");
                    body.AppendLine("</tr>");
                    
                    if (!string.IsNullOrEmpty(similarKaizen.OtherBenefits))
                    {
                        body.AppendLine("<tr>");
                        body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Benefits:</td>");
                        body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.OtherBenefits}</td>");
                        body.AppendLine("</tr>");
                    }
                    
                    if (similarKaizen.CostSaving.HasValue && similarKaizen.CostSaving > 0)
                    {
                        body.AppendLine("<tr>");
                        body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Cost Saving:</td>");
                        body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px; color: #27ae60; font-weight: bold;'>${similarKaizen.CostSaving:N2}</td>");
                        body.AppendLine("</tr>");
                    }
                    
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Status:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>");
                    
                    if (similarKaizen.EngineerStatus == "Approved" && similarKaizen.ManagerStatus == "Approved")
                    {
                        body.AppendLine("<span style='color: #27ae60; font-weight: bold;'>✅ Approved</span>");
                    }
                    else if (similarKaizen.EngineerStatus == "Rejected" || similarKaizen.ManagerStatus == "Rejected")
                    {
                        body.AppendLine("<span style='color: #e74c3c; font-weight: bold;'>❌ Rejected</span>");
                    }
                    else
                    {
                        body.AppendLine("<span style='color: #f39c12; font-weight: bold;'>⏳ Pending</span>");
                    }
                    
                    body.AppendLine("</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("</table>");
                    body.AppendLine("</div>");
                }

                body.AppendLine("<p><strong>💡 Tip:</strong> Reviewing similar suggestions can help you make informed decisions and identify patterns for improvement.</p>");
                body.AppendLine("<br/>");
            }

            body.AppendLine("<p>Please review this suggestion by clicking the link below:</p>");
            body.AppendLine($"<p><a href='{websiteUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>View Kaizen Suggestion</a></p>");
            body.AppendLine("<br/>");
            body.AppendLine("<p>Thank you for your attention to this matter.</p>");
            body.AppendLine("<p>Best regards,<br/>Kaizen Web Application Team</p>");
            body.AppendLine("</body></html>");

            return body.ToString();
        }

        public async Task<bool> SendInterDepartmentNotificationWithSimilarSuggestionsAsync(string toEmail, string kaizenNo, string employeeName, string sourceDepartment, string targetDepartment, string suggestionDescription, string websiteUrl, IEnumerable<KaizenWebApp.Models.KaizenForm> similarKaizens)
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
                        Subject = $"Inter-Department Kaizen Suggestion - {kaizenNo} (with Similar Suggestions)",
                        Body = GenerateInterDepartmentEmailBodyWithSimilarSuggestions(kaizenNo, employeeName, sourceDepartment, targetDepartment, suggestionDescription, websiteUrl, similarKaizens),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending inter-department email with similar suggestions: {ex.Message}");
                return false;
            }
        }

        private string GenerateInterDepartmentEmailBodyWithSimilarSuggestions(string kaizenNo, string employeeName, string sourceDepartment, string targetDepartment, string suggestionDescription, string websiteUrl, IEnumerable<KaizenWebApp.Models.KaizenForm> similarKaizens)
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

            // Add similar suggestions section if any found
            if (similarKaizens.Any())
            {
                body.AppendLine("<h3>📋 Similar Suggestions from Your Department:</h3>");
                body.AppendLine("<p><em>We found the following similar kaizen suggestions from your department that might be relevant for comparison:</em></p>");
                body.AppendLine("<br/>");

                foreach (var similarKaizen in similarKaizens)
                {
                    body.AppendLine("<div style='border: 1px solid #ddd; border-radius: 5px; padding: 15px; margin-bottom: 15px; background-color: #f9f9f9;'>");
                    body.AppendLine($"<h4 style='margin-top: 0; color: #2c3e50;'>📝 {similarKaizen.KaizenNo}</h4>");
                    body.AppendLine("<table style='border-collapse: collapse; width: 100%; font-size: 14px;'>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold; width: 30%;'>Submitted By:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.EmployeeName}</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Date:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.DateSubmitted:MMMM dd, yyyy}</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Description:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.SuggestionDescription}</td>");
                    body.AppendLine("</tr>");
                    
                    if (!string.IsNullOrEmpty(similarKaizen.OtherBenefits))
                    {
                        body.AppendLine("<tr>");
                        body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Benefits:</td>");
                        body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>{similarKaizen.OtherBenefits}</td>");
                        body.AppendLine("</tr>");
                    }
                    
                    if (similarKaizen.CostSaving.HasValue && similarKaizen.CostSaving > 0)
                    {
                        body.AppendLine("<tr>");
                        body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Cost Saving:</td>");
                        body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px; color: #27ae60; font-weight: bold;'>${similarKaizen.CostSaving:N2}</td>");
                        body.AppendLine("</tr>");
                    }
                    
                    body.AppendLine("<tr>");
                    body.AppendLine("<td style='border: 1px solid #ddd; padding: 6px; font-weight: bold;'>Status:</td>");
                    body.AppendLine($"<td style='border: 1px solid #ddd; padding: 6px;'>");
                    
                    if (similarKaizen.EngineerStatus == "Approved" && similarKaizen.ManagerStatus == "Approved")
                    {
                        body.AppendLine("<span style='color: #27ae60; font-weight: bold;'>✅ Approved</span>");
                    }
                    else if (similarKaizen.EngineerStatus == "Rejected" || similarKaizen.ManagerStatus == "Rejected")
                    {
                        body.AppendLine("<span style='color: #e74c3c; font-weight: bold;'>❌ Rejected</span>");
                    }
                    else
                    {
                        body.AppendLine("<span style='color: #f39c12; font-weight: bold;'>⏳ Pending</span>");
                    }
                    
                    body.AppendLine("</td>");
                    body.AppendLine("</tr>");
                    body.AppendLine("</table>");
                    body.AppendLine("</div>");
                }

                body.AppendLine("<p><strong>💡 Tip:</strong> Reviewing similar suggestions from your department can help you understand how this inter-department suggestion might be adapted for your area.</p>");
                body.AppendLine("<br/>");
            }

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
    }
}
