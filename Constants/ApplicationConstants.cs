namespace KaizenWebApp.Constants
{
    public static class ApplicationConstants
    {
        // File upload constants
        public const int MaxFileSizeInBytes = 5 * 1024 * 1024; // 5MB
        public const string UploadsFolder = "uploads";
        public const string ImagesFolder = "images";
        public static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

        // Pagination constants
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 100;

        // Session constants
        public const int SessionTimeoutMinutes = 480; // 8 hours
        public const string SessionKeyPrefix = "Kaizen_";

        // Status constants
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";

        // User role constants
        public const string RoleAdmin = "Admin";
        public const string RoleUser = "User";
        public const string RoleManager = "Manager";
        public const string RoleEngineer = "Engineer";
        public const string RoleKaizenTeam = "KaizenTeam";

        // Kaizen number format
        public const string KaizenNumberPrefix = "KZN";
        public const string KaizenNumberFormat = "yyyyMMdd";

        // Validation constants
        public const int MaxEmployeeNameLength = 100;
        public const int MaxEmployeeNumberLength = 20;
        public const int MaxDepartmentLength = 100;
        public const int MaxSuggestionDescriptionLength = 1000;
        public const int MaxCommentsLength = 1000;
        public const int MaxCategoryLength = 500;
        public const int MaxImplementationAreaLength = 500;

        // Cost saving constants
        public const decimal MaxCostSaving = 999999999.99m;
        public const string CostSavingTypeNoSaving = "NoCostSaving";
        public const string CostSavingTypeHasSaving = "HasCostSaving";

        // Award constants
        public const string AwardFirstPrice = "1ST PRICE";
        public const string AwardSecondPrice = "2ND PRICE";
        public const string AwardThirdPrice = "3RD PRICE";
        public const string AwardNoPrice = "NO PRICE";

        // Date formats
        public const string DateFormat = "MM/dd/yyyy";
        public const string DateTimeFormat = "MM/dd/yyyy HH:mm:ss";
        public const string DateFormatForDisplay = "MMMM dd, yyyy";

        // Error messages
        public const string ErrorMessageGeneric = "An error occurred. Please try again.";
        public const string ErrorMessageFileUpload = "File upload failed. Please check the file size and format.";
        public const string ErrorMessageUnauthorized = "You are not authorized to perform this action.";
        public const string ErrorMessageNotFound = "The requested resource was not found.";

        // Success messages
        public const string SuccessMessageKaizenCreated = "Kaizen suggestion created successfully.";
        public const string SuccessMessageKaizenUpdated = "Kaizen suggestion updated successfully.";
        public const string SuccessMessageKaizenDeleted = "Kaizen suggestion deleted successfully.";
        public const string SuccessMessageUserCreated = "User created successfully.";
        public const string SuccessMessageUserUpdated = "User updated successfully.";

        // Cache keys
        public const string CacheKeyDepartments = "Departments";
        public const string CacheKeyUserStats = "UserStats";
        public const string CacheKeyKaizenStats = "KaizenStats";
        public const int CacheExpirationMinutes = 30;

        // API endpoints (if needed for future API development)
        public const string ApiBasePath = "/api";
        public const string ApiVersion = "v1";
    }
}
