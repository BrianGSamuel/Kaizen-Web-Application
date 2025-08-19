using KaizenWebApp.Models;

namespace KaizenWebApp.Services
{
    public interface IUserService
    {
        Task<Users?> GetUserByUsernameAsync(string username);
        Task<Users?> GetUserByIdAsync(int id);
        Task<IEnumerable<Users>> GetAllUsersAsync();

        Task<bool> DeleteUserAsync(int id);
        Task<bool> ValidateUserCredentialsAsync(string username, string password);
        Task<bool> UserExistsAsync(string username);
        Task<string?> GetUserDepartmentAsync(string username);
        Task<int> GetTotalUsersCountAsync();
    }
}
