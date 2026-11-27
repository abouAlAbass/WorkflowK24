using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalOidcAuthProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthProviders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Protocol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "oidc"),
                    Authority = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EncryptedClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "openid profile email"),
                    RoleMappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    JitProvisioningEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AllowEmailMatching = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AllowAdminRoleMapping = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DefaultRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "User"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalUserLogins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalSubject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalUserLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalUserLogins_AuthProviders",
                        column: x => x.ProviderId,
                        principalTable: "AuthProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalUserLogins_SystemUsers",
                        column: x => x.UserId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AuthProviders_Name",
                table: "AuthProviders",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalUserLogins_UserId",
                table: "ExternalUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExternalUserLogins_Provider_Sub",
                table: "ExternalUserLogins",
                columns: new[] { "ProviderId", "ExternalSubject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalUserLogins");

            migrationBuilder.DropTable(
                name: "AuthProviders");
        }
    }
}
