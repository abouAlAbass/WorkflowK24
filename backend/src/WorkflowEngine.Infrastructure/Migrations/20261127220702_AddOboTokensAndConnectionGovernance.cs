using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOboTokensAndConnectionGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HealthStatus",
                table: "Connections",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "LastErrorSanitized",
                table: "Connections",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRotatedAt",
                table: "Connections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestedAt",
                table: "Connections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "Connections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SecretExpiresAt",
                table: "Connections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConnectionEnvironmentConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnvironmentName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BaseUrlOverride = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedCredentialsJsonOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalSecretKeyOverride = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionEnvironmentConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionEnvironmentConfigs_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionUsageLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkflowKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionUsageLogs_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalUserTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalUserTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ConnectionEnvironmentConfigs_Connection_Env",
                table: "ConnectionEnvironmentConfigs",
                columns: new[] { "ConnectionId", "EnvironmentName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionUsageLogs_Connection_ExecutedAt",
                table: "ConnectionUsageLogs",
                columns: new[] { "ConnectionId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalUserTokens_UserId",
                table: "ExternalUserTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExternalUserTokens_User_Provider",
                table: "ExternalUserTokens",
                columns: new[] { "UserId", "ProviderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionEnvironmentConfigs");

            migrationBuilder.DropTable(
                name: "ConnectionUsageLogs");

            migrationBuilder.DropTable(
                name: "ExternalUserTokens");

            migrationBuilder.DropColumn(
                name: "HealthStatus",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "LastErrorSanitized",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "LastRotatedAt",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "LastTestedAt",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "SecretExpiresAt",
                table: "Connections");
        }
    }
}
