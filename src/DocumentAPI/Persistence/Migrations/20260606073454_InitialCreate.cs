using System;
using DocumentAPI.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentAPI.Persistence.Migrations
{
    /// <summary>
    /// Baseline migration that creates the initial relational schema for document metadata.
    /// This file contains the executable database operations used when applying or rolling back the migration.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <summary>
        /// Applies the initial schema by creating the documents table and its unique content-hash index.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: PersistenceModelConstants.DefaultSchema);

            migrationBuilder.CreateTable(
                name: PersistenceModelConstants.DocumentsTable,
                schema: PersistenceModelConstants.DefaultSchema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ContentHash",
                schema: PersistenceModelConstants.DefaultSchema,
                table: PersistenceModelConstants.DocumentsTable,
                column: "ContentHash",
                unique: true);
        }

        /// <summary>
        /// Reverts the initial schema by dropping the documents table.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: PersistenceModelConstants.DocumentsTable,
                schema: PersistenceModelConstants.DefaultSchema);
        }
    }
}
