using KaizenWebApp.Data;
using KaizenWebApp.Models;
using KaizenWebApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaizenWebApp.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Users?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == username);
        }

        public async Task<Users?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<IEnumerable<Users>> GetAllUsersAsync()
        {
            return await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();
        }

        public async Task<Users> CreateUserAsync(RegisterViewModel model)
        {
            var user = new Users
            {
                UserName = model.Username,
                DepartmentName = model.Department,
                Plant = model.Plant,
                Password = model.Password // TODO: Hash password in production
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User created: {Username} with ID {UserId}", user.UserName, user.Id);
            
            return user;
        }

        public async Task<bool> UpdateUserAsync(int id, RegisterViewModel model)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.UserName = model.Username;
            user.DepartmentName = model.Department;
            user.Plant = model.Plant;
            user.Password = model.Password; // TODO: Hash password in production

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUserCredentialsAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user == null) return false;

            // TODO: Use proper password hashing in production
            return user.Password == password;
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.UserName == username);
        }

        public async Task<string?> GetUserDepartmentAsync(string username)
        {
            var user = await GetUserByUsernameAsync(username);
            return user?.DepartmentName;
        }

        public async Task<int> GetTotalUsersCountAsync()
        {
            return await _context.Users.CountAsync();
        }
    }
}
