using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartFormSubmissionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SmartFormSubmissions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Submitted");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SmartFormSubmissions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateIndex(
                name: "IX_SmartFormSubmissions_SubmittedBy_Status",
                table: "SmartFormSubmissions",
                columns: new[] { "SubmittedBy", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmartFormSubmissions_SubmittedBy_Status",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SmartFormSubmissions");
        }
    }
}
