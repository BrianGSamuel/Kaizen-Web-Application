namespace KaizenWebApp.Utilities
{
    public static class ValidationHelper
    {
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Basic phone number validation (can be enhanced based on requirements)
            return System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^[\+]?[1-9][\d]{0,15}$");
        }

        public static bool IsValidEmployeeNumber(string employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo))
                return false;

            // Employee number should be alphanumeric and 3-10 characters
            return System.Text.RegularExpressions.Regex.IsMatch(employeeNo, @"^[A-Za-z0-9]{3,10}$");
        }

        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Basic HTML encoding to prevent XSS
            return System.Web.HttpUtility.HtmlEncode(input.Trim());
        }

        public static bool IsValidCostSaving(decimal? costSaving)
        {
            return !costSaving.HasValue || costSaving.Value >= 0;
        }

        public static string FormatCurrency(decimal amount)
        {
            return amount.ToString("C2");
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("MM/dd/yyyy");
        }

        public static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("MM/dd/yyyy HH:mm:ss");
        }

        public static string GetFileExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            return Path.GetExtension(fileName).ToLowerInvariant();
        }

        public static bool IsImageFile(string fileName)
        {
            var extension = GetFileExtension(fileName);
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
            return allowedExtensions.Contains(extension);
        }

        public static string GenerateRandomString(int length = 8)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string TruncateText(string text, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - suffix.Length) + suffix;
        }
    }
}
