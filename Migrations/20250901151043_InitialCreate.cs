using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetCount = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gallery", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarkingCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriteriaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkingCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecipientRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientUsername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemMaintenance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsSystemOffline = table.Column<bool>(type: "bit", nullable: false),
                    MaintenanceMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaintenanceStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaintenanceEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemMaintenance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Plant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeePhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KaizenForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KaizenNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateSubmitted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateImplemented = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Plant = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EmployeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmployeePhotoPath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SuggestionDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CostSaving = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostSavingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DollarRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherBenefits = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeKaizenImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AfterKaizenImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CanImplementInOtherFields = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ImplementationArea = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManagerComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ManagerSignature = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EngineerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EngineerApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManagerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManagerApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AwardPrice = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CommitteeComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CommitteeSignature = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AwardDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InterDeptStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InterDeptApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InterDeptApprovedDepartments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InterDeptRejectedDepartments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KaizenForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KaizenForms_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KaizenMarkingScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KaizenId = table.Column<int>(type: "int", nullable: false),
                    MarkingCriteriaId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KaizenMarkingScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KaizenMarkingScores_KaizenForms_KaizenId",
                        column: x => x.KaizenId,
                        principalTable: "KaizenForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KaizenMarkingScores_MarkingCriteria_MarkingCriteriaId",
                        column: x => x.MarkingCriteriaId,
                        principalTable: "MarkingCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KaizenForms_CategoryId",
                table: "KaizenForms",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KaizenMarkingScores_KaizenId",
                table: "KaizenMarkingScores",
                column: "KaizenId");

            migrationBuilder.CreateIndex(
                name: "IX_KaizenMarkingScores_MarkingCriteriaId",
                table: "KaizenMarkingScores",
                column: "MarkingCriteriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentTargets");

            migrationBuilder.DropTable(
                name: "Gallery");

            migrationBuilder.DropTable(
                name: "KaizenMarkingScores");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SystemMaintenance");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "KaizenForms");

            migrationBuilder.DropTable(
                name: "MarkingCriteria");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
