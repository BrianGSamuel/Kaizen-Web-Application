using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparateApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EngineerApprovedBy",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineerStatus",
                table: "KaizenForms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerApprovedBy",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerStatus",
                table: "KaizenForms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Add ManagerComments and ManagerSignature only if they don't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerComments')
                BEGIN
                    ALTER TABLE [KaizenForms] ADD [ManagerComments] nvarchar(1000) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerSignature')
                BEGIN
                    ALTER TABLE [KaizenForms] ADD [ManagerSignature] nvarchar(100) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EngineerApprovedBy",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "EngineerStatus",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ManagerApprovedBy",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ManagerStatus",
                table: "KaizenForms");

            // Drop ManagerComments and ManagerSignature only if they exist
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerComments')
                BEGIN
                    ALTER TABLE [KaizenForms] DROP COLUMN [ManagerComments];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerSignature')
                BEGIN
                    ALTER TABLE [KaizenForms] DROP COLUMN [ManagerSignature];
                END
            ");
        }
    }
}
