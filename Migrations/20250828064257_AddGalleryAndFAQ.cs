using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryAndFAQ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if Description column exists before dropping it
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Categories' 
                    AND COLUMN_NAME = 'Description'
                )
                BEGIN
                    ALTER TABLE Categories DROP COLUMN Description
                END
            ");

            // Check if FAQs table already exists before creating it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FAQs' AND xtype='U')
                BEGIN
                    CREATE TABLE [FAQs] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [Question] nvarchar(200) NOT NULL,
                        [Answer] nvarchar(2000) NOT NULL,
                        [Category] nvarchar(50) NULL,
                        [DisplayOrder] int NOT NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedDate] datetime2 NOT NULL,
                        [UpdatedDate] datetime2 NULL,
                        CONSTRAINT [PK_FAQs] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Check if Gallery table already exists before creating it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Gallery' AND xtype='U')
                BEGIN
                    CREATE TABLE [Gallery] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [Title] nvarchar(100) NOT NULL,
                        [Description] nvarchar(500) NULL,
                        [ImagePath] nvarchar(500) NOT NULL,
                        [UploadDate] datetime2 NOT NULL,
                        [DisplayOrder] int NOT NULL,
                        [IsActive] bit NOT NULL,
                        CONSTRAINT [PK_Gallery] PRIMARY KEY ([Id])
                    )
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FAQs");

            migrationBuilder.DropTable(
                name: "Gallery");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
