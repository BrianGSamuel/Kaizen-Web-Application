using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class MakeOptionalFieldsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostSavingType",
                table: "KaizenForms",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DollarRate",
                table: "KaizenForms",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostSavingType",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "DollarRate",
                table: "KaizenForms");
        }
    }
}
