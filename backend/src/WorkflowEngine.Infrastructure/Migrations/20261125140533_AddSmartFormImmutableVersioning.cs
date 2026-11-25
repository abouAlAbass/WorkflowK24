using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartFormImmutableVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmartFormVersionId",
                table: "WorkflowInstances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmartFormVersionId",
                table: "SmartFormSubmissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivePublishedVersionId",
                table: "SmartForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestVersionNumber",
                table: "SmartForms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "SmartFormVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SmartFormId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartFormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartFormVersions_SmartForms",
                        column: x => x.SmartFormId,
                        principalTable: "SmartForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmartFormSubmissions_VersionId",
                table: "SmartFormSubmissions",
                column: "SmartFormVersionId");

            migrationBuilder.CreateIndex(
                name: "UQ_SmartFormVersions_Form_Ver",
                table: "SmartFormVersions",
                columns: new[] { "SmartFormId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartFormVersions");

            migrationBuilder.DropIndex(
                name: "IX_SmartFormSubmissions_VersionId",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "SmartFormVersionId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "SmartFormVersionId",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "ActivePublishedVersionId",
                table: "SmartForms");

            migrationBuilder.DropColumn(
                name: "LatestVersionNumber",
                table: "SmartForms");
        }
    }
}
