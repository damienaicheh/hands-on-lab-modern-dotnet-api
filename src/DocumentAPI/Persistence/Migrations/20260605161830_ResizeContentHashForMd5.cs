using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentAPI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResizeContentHashForMd5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                schema: "dbo",
                table: "Documents",
                type: "nchar(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(64)",
                oldFixedLength: true,
                oldMaxLength: 64);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                schema: "dbo",
                table: "Documents",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(32)",
                oldFixedLength: true,
                oldMaxLength: 32);
        }
    }
}
