using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Remove_DocumentExtractedField_DataType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "PaperbaseDocumentExtractedFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DataType",
                table: "PaperbaseDocumentExtractedFields",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
