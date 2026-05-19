using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class FieldArchitectureV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperbaseDocumentTenantFields");

            migrationBuilder.DropTable(
                name: "PaperbaseTenantFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "SystemFieldsJson",
                table: "PaperbaseDocuments");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedFields",
                table: "PaperbaseDocuments",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "PaperbaseDocuments",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OcrConfidence",
                table: "PaperbaseDocuments",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaperbaseDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TypeCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConfidenceThreshold = table.Column<double>(type: "float", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperbaseDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperbaseFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperbaseFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentTypes_TenantId_TypeCode",
                table: "PaperbaseDocumentTypes",
                columns: new[] { "TenantId", "TypeCode" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseFieldDefinitions_TenantId_DocumentTypeCode",
                table: "PaperbaseFieldDefinitions",
                columns: new[] { "TenantId", "DocumentTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseFieldDefinitions_TenantId_DocumentTypeCode_Name",
                table: "PaperbaseFieldDefinitions",
                columns: new[] { "TenantId", "DocumentTypeCode", "Name" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperbaseDocumentTypes");

            migrationBuilder.DropTable(
                name: "PaperbaseFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "ExtractedFields",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "PaperbaseDocuments");

            migrationBuilder.DropColumn(
                name: "OcrConfidence",
                table: "PaperbaseDocuments");

            migrationBuilder.AddColumn<string>(
                name: "SystemFieldsJson",
                table: "PaperbaseDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaperbaseDocumentTenantFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperbaseDocumentTenantFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperbaseTenantFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperbaseTenantFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentTenantFields_TenantId_DocumentId_FieldName",
                table: "PaperbaseDocumentTenantFields",
                columns: new[] { "TenantId", "DocumentId", "FieldName" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseDocumentTenantFields_TenantId_FieldName",
                table: "PaperbaseDocumentTenantFields",
                columns: new[] { "TenantId", "FieldName" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseTenantFieldDefinitions_TenantId_DocumentTypeCode",
                table: "PaperbaseTenantFieldDefinitions",
                columns: new[] { "TenantId", "DocumentTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperbaseTenantFieldDefinitions_TenantId_DocumentTypeCode_Name",
                table: "PaperbaseTenantFieldDefinitions",
                columns: new[] { "TenantId", "DocumentTypeCode", "Name" },
                unique: true,
                filter: "IsDeleted = 0");
        }
    }
}
