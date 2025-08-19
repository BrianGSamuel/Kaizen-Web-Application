using KaizenWebApp.Data;
using KaizenWebApp.Models;

namespace KaizenWebApp.Services
{
    public class DatabaseSeedService
    {
        private readonly AppDbContext _context;

        public DatabaseSeedService(AppDbContext context)
        {
            _context = context;
        }

        public async Task SeedDatabaseAsync()
        {
            await SeedAdminUserAsync();
        }

        private async Task SeedAdminUserAsync()
        {
            // Check if admin user already exists
            var existingAdmin = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "Admin");
            
            if (existingAdmin == null)
            {
                var adminUser = new Users
                {
                    DepartmentName = "Administration",
                    Plant = "Main Plant",
                    UserName = "Admin",
                    Password = "admin123",
                    Role = "Admin",
                    EmployeeName = "System Administrator",
                    EmployeeNumber = "ADMIN001",
                    Email = "admin@company.com"
                };

                await _context.Users.AddAsync(adminUser);
                await _context.SaveChangesAsync();
                
                Console.WriteLine("✅ Admin user created successfully!");
                Console.WriteLine("   Username: Admin");
                Console.WriteLine("   Password: admin123");
                Console.WriteLine("   Role: Admin");
            }
            else
            {
                Console.WriteLine("ℹ️  Admin user already exists in the database.");
            }
        }
    }
}

