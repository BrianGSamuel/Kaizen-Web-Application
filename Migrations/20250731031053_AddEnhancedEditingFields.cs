using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedEditingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualCompletionDate",
                table: "KaizenForms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDate",
                table: "KaizenForms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedOutcomes",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImplementationPlan",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemStatement",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectLeader",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectTitle",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskAssessment",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Solution",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuccessMetrics",
                table: "KaizenForms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetCompletionDate",
                table: "KaizenForms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamMembers",
                table: "KaizenForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualCompletionDate",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ApprovalDate",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ExpectedOutcomes",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ImplementationPlan",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ProblemStatement",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ProjectLeader",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "ProjectTitle",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "RiskAssessment",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "Solution",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "SuccessMetrics",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "TargetCompletionDate",
                table: "KaizenForms");

            migrationBuilder.DropColumn(
                name: "TeamMembers",
                table: "KaizenForms");
        }
    }
}
