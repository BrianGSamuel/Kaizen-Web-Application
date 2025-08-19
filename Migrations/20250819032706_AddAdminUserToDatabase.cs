using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminUserToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert admin user if it doesn't already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Users WHERE UserName = 'Admin')
                BEGIN
                    INSERT INTO Users (
                        DepartmentName,
                        Plant,
                        UserName,
                        Password,
                        Role,
                        EmployeeName,
                        EmployeeNumber
                    ) VALUES (
                        'Administration',
                        'Main Plant',
                        'Admin',
                        'admin123',
                        'Admin',
                        'System Administrator',
                        'ADMIN001'
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove admin user if migration is rolled back
            migrationBuilder.Sql(@"
                DELETE FROM Users WHERE UserName = 'Admin'
            ");
        }
    }
}
