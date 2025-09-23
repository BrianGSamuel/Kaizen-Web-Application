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
        public DbSet<SystemMaintenance> SystemMaintenance { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<MarkingCriteria> MarkingCriteria { get; set; }
        public DbSet<KaizenMarkingScore> KaizenMarkingScores { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Gallery> Gallery { get; set; }
        public DbSet<AwardThreshold> AwardThresholds { get; set; }
    }
}