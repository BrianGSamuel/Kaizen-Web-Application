namespace KaizenWebApp.Configuration
{
    public class AppSettings
    {
        public FileUploadSettings FileUpload { get; set; } = new();
        public AuthenticationSettings Authentication { get; set; } = new();
        public DatabaseSettings Database { get; set; } = new();
    }

    public class FileUploadSettings
    {
        public int MaxFileSizeInMB { get; set; } = 5;
        public string[] AllowedImageExtensions { get; set; } = { ".png", ".jpg", ".jpeg", ".webp" };
        public string UploadsFolder { get; set; } = "uploads";
    }

    public class AuthenticationSettings
    {
        public int SessionTimeoutHours { get; set; } = 8;
        public bool SlidingExpiration { get; set; } = true;
        public string LoginPath { get; set; } = "/Account/Login";
        public string AccessDeniedPath { get; set; } = "/Home/AccessDenied";
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public bool EnableSensitiveDataLogging { get; set; } = false;
    }
}
