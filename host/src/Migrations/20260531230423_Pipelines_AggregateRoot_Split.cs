using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Pipelines_AggregateRoot_Split : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaperbaseDocumentPipelineRuns_DocumentId_PipelineCode_AttemptNumber",
                table: "PaperbaseDocumentPipelineRuns");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "PaperbaseDocumentPipelineRuns",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentPipelineRuns_DocumentId_PipelineCode_AttemptNumber",
                table: "PaperbaseDocumentPipelineRuns",
                columns: new[] { "DocumentId", "PipelineCode", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaperbaseDocumentPipelineRuns_DocumentId_PipelineCode_AttemptNumber",
                table: "PaperbaseDocumentPipelineRuns");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "PaperbaseDocumentPipelineRuns");

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentPipelineRuns_DocumentId_PipelineCode_AttemptNumber",
                table: "PaperbaseDocumentPipelineRuns",
                columns: new[] { "DocumentId", "PipelineCode", "AttemptNumber" });
        }
    }
}
