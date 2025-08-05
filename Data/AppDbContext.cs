using Microsoft.EntityFrameworkCore;
using KaizenWebApp.Models;

namespace KaizenWebApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Users> Users { get; set; }
        public DbSet<KaizenForm> KaizenForms { get; set; }
        public DbSet<DepartmentTarget> DepartmentTargets { get; set; }
    }
}