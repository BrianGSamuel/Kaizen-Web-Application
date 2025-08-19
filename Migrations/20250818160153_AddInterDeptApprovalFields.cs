using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddInterDeptApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InterDeptApprovedBy",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterDeptApprovedDepartments",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterDeptRejectedDepartments",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterDeptStatus",
                table: "KaizenForms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterDeptApprovedBy",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "InterDeptApprovedDepartments",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "InterDeptRejectedDepartments",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "InterDeptStatus",
                table: "KaizenForms");
        }
    }
}
