namespace KaizenWebApp.Services
{
    public interface IFileService
    {
        Task<string?> SaveImageAsync(IFormFile file, string folderName);
        Task<bool> DeleteFileAsync(string filePath);
        Task<bool> IsValidImageAsync(IFormFile file);
        Task<byte[]?> GetFileBytesAsync(string filePath);
        Task<bool> FileExistsAsync(string filePath);
    }
}
