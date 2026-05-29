using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Limit_DocumentExtractedField_StringValue_Length : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StringValue",
                table: "PaperbaseDocumentExtractedFields",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_StringValue_DocumentId",
                table: "PaperbaseDocumentExtractedFields",
                columns: new[] { "TenantId", "FieldDefinitionId", "StringValue", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_StringValue_DocumentId",
                table: "PaperbaseDocumentExtractedFields");

            migrationBuilder.AlterColumn<string>(
                name: "StringValue",
                table: "PaperbaseDocumentExtractedFields",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
