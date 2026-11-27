using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartObjectSourceLinkAndPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryKeyField",
                table: "SmartObjects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceConnectionId",
                table: "SmartObjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEntityName",
                table: "SmartObjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimaryKeyField",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "SourceConnectionId",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "SourceEntityName",
                table: "SmartObjects");
        }
    }
}
