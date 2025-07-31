using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutiveEngineerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanImplementInOtherFields",
                table: "KaizenForms",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImplementationArea",
                table: "KaizenForms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanImplementInOtherFields",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ImplementationArea",
                table: "KaizenForms");
        }
    }
}
