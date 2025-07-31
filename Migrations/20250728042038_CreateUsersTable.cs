using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class CreateUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KaizenForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KaizenNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateSubmitted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateImplemented = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestionDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeKaizen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterKaizen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostSaving = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherBenefits = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeKaizenImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterKaizenImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KaizenForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KaizenForms");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
