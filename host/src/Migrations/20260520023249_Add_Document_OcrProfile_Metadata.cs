using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Add_Document_OcrProfile_Metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EffectiveOcrProfileCode",
                table: "PaperbaseDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProfileResolutionReason",
                table: "PaperbaseDocuments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProviderModelName",
                table: "PaperbaseDocuments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProviderName",
                table: "PaperbaseDocuments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProviderVersion",
                table: "PaperbaseDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrQualitySignals",
                table: "PaperbaseDocuments",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedOcrProfileCode",
                table: "PaperbaseDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveOcrProfileCode",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrProfileResolutionReason",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrProviderModelName",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrProviderName",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrProviderVersion",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrQualitySignals",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "RequestedOcrProfileCode",
                table: "PaperbaseDocuments");
        }
    }
}
