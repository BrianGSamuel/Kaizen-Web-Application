using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AwardDate",
                table: "KaizenForms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwardPrice",
                table: "KaizenForms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitteeComments",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitteeSignature",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwardDate",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "AwardPrice",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "CommitteeComments",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "CommitteeSignature",
                table: "KaizenForms");
        }
    }
}
