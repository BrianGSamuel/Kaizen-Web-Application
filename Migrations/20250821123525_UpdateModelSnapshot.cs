using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaizenWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if CategoryId column already exists before adding it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'KaizenForms' 
                    AND COLUMN_NAME = 'CategoryId'
                )
                BEGIN
                    ALTER TABLE KaizenForms ADD CategoryId int NULL
                END
            ");

            // Check if Categories table already exists before creating it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                BEGIN
                    CREATE TABLE [Categories] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [Name] nvarchar(100) NOT NULL,
                        [Description] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [IsActive] bit NOT NULL,
                        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Check if MarkingCriteria table already exists before creating it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MarkingCriteria' AND xtype='U')
                BEGIN
                    CREATE TABLE [MarkingCriteria] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [CriteriaName] nvarchar(200) NOT NULL,
                        [Description] nvarchar(1000) NOT NULL,
                        [MaxScore] int NOT NULL,
                        [Weight] int NOT NULL,
                        [Category] nvarchar(50) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [Notes] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(100) NULL,
                        [UpdatedBy] nvarchar(100) NULL,
                        CONSTRAINT [PK_MarkingCriteria] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Check if KaizenMarkingScores table already exists before creating it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='KaizenMarkingScores' AND xtype='U')
                BEGIN
                    CREATE TABLE [KaizenMarkingScores] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [KaizenId] int NOT NULL,
                        [MarkingCriteriaId] int NOT NULL,
                        [Score] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        CONSTRAINT [PK_KaizenMarkingScores] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Create indexes and foreign keys if they don't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_KaizenForms_CategoryId')
                BEGIN
                    CREATE INDEX [IX_KaizenForms_CategoryId] ON [KaizenForms] ([CategoryId])
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_KaizenMarkingScores_KaizenId')
                BEGIN
                    CREATE INDEX [IX_KaizenMarkingScores_KaizenId] ON [KaizenMarkingScores] ([KaizenId])
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_KaizenMarkingScores_MarkingCriteriaId')
                BEGIN
                    CREATE INDEX [IX_KaizenMarkingScores_MarkingCriteriaId] ON [KaizenMarkingScores] ([MarkingCriteriaId])
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_KaizenForms_Categories_CategoryId')
                BEGIN
                    ALTER TABLE [KaizenForms] ADD CONSTRAINT [FK_KaizenForms_Categories_CategoryId] 
                    FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KaizenForms_Categories_CategoryId",
                table: "KaizenForms");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "KaizenMarkingScores");

            migrationBuilder.DropTable(
                name: "MarkingCriteria");

            migrationBuilder.DropIndex(
                name: "IX_KaizenForms_CategoryId",
                table: "KaizenForms");

            // Check if CategoryId column exists before dropping it
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'KaizenForms' 
                    AND COLUMN_NAME = 'CategoryId'
                )
                BEGIN
                    ALTER TABLE KaizenForms DROP COLUMN CategoryId
                END
            ");
        }
    }
}
