using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingRecordsToPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE KaizenForms SET Status = 'Pending' WHERE Status = '' OR Status IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
