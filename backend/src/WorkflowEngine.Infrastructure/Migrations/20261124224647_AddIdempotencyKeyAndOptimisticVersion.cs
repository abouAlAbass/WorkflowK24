using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeyAndOptimisticVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WorkflowTasks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SmartFormSubmissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SmartForms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "UX_SmartFormSubmissions_Form_IdempotencyKey",
                table: "SmartFormSubmissions",
                columns: new[] { "SmartFormId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_SmartFormSubmissions_Form_IdempotencyKey",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SmartForms");
        }
    }
}
