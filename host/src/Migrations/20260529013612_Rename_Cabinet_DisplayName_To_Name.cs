using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Rename_Cabinet_DisplayName_To_Name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "PaperbaseCabinets",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_PaperbaseCabinets_TenantId_DisplayName",
                table: "PaperbaseCabinets",
                newName: "IX_PaperbaseCabinets_TenantId_Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "PaperbaseCabinets",
                newName: "DisplayName");

            migrationBuilder.RenameIndex(
                name: "IX_PaperbaseCabinets_TenantId_Name",
                table: "PaperbaseCabinets",
                newName: "IX_PaperbaseCabinets_TenantId_DisplayName");
        }
    }
}
