using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionsPermissionsAndUserVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "WorkflowTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                table: "Connections",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "AuthMode",
                table: "Connections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "static");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedCredentialsJson",
                table: "Connections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSecretKey",
                table: "Connections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecretSource",
                table: "Connections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "internal_encrypted");

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "Connections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "rest");

            migrationBuilder.CreateTable(
                name: "ConnectionPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CanUse = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanManage = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionPermissions_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserConnectionCredentials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EncryptedSecretJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConnectionCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConnectionCredentials_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ConnectionPermissions_Connection_Subject",
                table: "ConnectionPermissions",
                columns: new[] { "ConnectionId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConnectionCredentials_ConnectionId",
                table: "UserConnectionCredentials",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "UQ_UserConnectionCredentials_User_Connection",
                table: "UserConnectionCredentials",
                columns: new[] { "UserId", "ConnectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionPermissions");

            migrationBuilder.DropTable(
                name: "UserConnectionCredentials");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "AuthMode",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "EncryptedCredentialsJson",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ExternalSecretKey",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "SecretSource",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "Connections");

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                table: "Connections",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldDefaultValue: "");
        }
    }
}
