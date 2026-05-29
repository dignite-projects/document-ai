using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Rename_DocumentExtractedField_DecimalValue_To_NumberValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DecimalValue",
                table: "PaperbaseDocumentExtractedFields",
                newName: "NumberValue");

            migrationBuilder.RenameIndex(
                name: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_DecimalValue_DocumentId",
                table: "PaperbaseDocumentExtractedFields",
                newName: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_NumberValue_DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NumberValue",
                table: "PaperbaseDocumentExtractedFields",
                newName: "DecimalValue");

            migrationBuilder.RenameIndex(
                name: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_NumberValue_DocumentId",
                table: "PaperbaseDocumentExtractedFields",
                newName: "IX_PaperbaseDocumentExtractedFields_TenantId_FieldDefinitionId_DecimalValue_DocumentId");
        }
    }
}
