using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Add_DocumentExtractedField_Order_And_FieldDefinition_AllowMultiple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PaperbaseDocumentExtractedFields",
                table: "PaperbaseDocumentExtractedFields");

            migrationBuilder.AddColumn<bool>(
                name: "AllowMultiple",
                table: "PaperbaseFieldDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "PaperbaseDocumentExtractedFields",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaperbaseDocumentExtractedFields",
                table: "PaperbaseDocumentExtractedFields",
                columns: new[] { "DocumentId", "FieldDefinitionId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PaperbaseDocumentExtractedFields",
                table: "PaperbaseDocumentExtractedFields");

            // 回滚安全（#212）：旧 PK 是 (DocumentId, FieldDefinitionId)，容不下多值行。一旦本特性被用过，
            // 多值字段会有同 (DocumentId, FieldDefinitionId) 的多行（Order 0,1,2…）。若直接 DropColumn(Order) 后恢复窄 PK，
            // 这些行会塌成重复键、AddPrimaryKey 失败——恰在需要回滚时炸。故先删掉额外行（Order <> 0），只保留 Order 0 那行。
            // 这是回滚一个"新增多值能力"的迁移的固有取舍：降级即丢多值（目标 schema 本就无法表示多值）。必须在 DropColumn(Order) 之前执行。
            migrationBuilder.Sql(
                "DELETE FROM [PaperbaseDocumentExtractedFields] WHERE [Order] <> 0;");

            migrationBuilder.DropColumn(
                name: "AllowMultiple",
                table: "PaperbaseFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "PaperbaseDocumentExtractedFields");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaperbaseDocumentExtractedFields",
                table: "PaperbaseDocumentExtractedFields",
                columns: new[] { "DocumentId", "FieldDefinitionId" });
        }
    }
}
