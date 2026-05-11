using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Paperbase.Host.Migrations
{
    /// <inheritdoc />
    public partial class Add_SoftDelete_To_DocumentRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "PaperbaseDocumentRelations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "PaperbaseDocumentRelations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PaperbaseDocumentRelations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "PaperbaseDocumentRelations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifierId",
                table: "PaperbaseDocumentRelations",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "PaperbaseDocumentRelations");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "PaperbaseDocumentRelations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PaperbaseDocumentRelations");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "PaperbaseDocumentRelations");

            migrationBuilder.DropColumn(
                name: "LastModifierId",
                table: "PaperbaseDocumentRelations");
        }
    }
}
