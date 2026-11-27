using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParallelTokensAndJoinState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "WorkflowTasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenId",
                table: "WorkflowTasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JoinStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SplitNodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JoinNodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    ArrivedCount = table.Column<int>(type: "int", nullable: false),
                    JoinFired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinStates_Instances",
                        column: x => x.InstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "InstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParentTokenId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SplitNodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BranchVariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTokens_Instances",
                        column: x => x.InstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "InstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JoinStates_Instance_Split_Join",
                table: "JoinStates",
                columns: new[] { "InstanceId", "SplitNodeId", "JoinNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTokens_Instance_Split_Branch",
                table: "WorkflowTokens",
                columns: new[] { "InstanceId", "SplitNodeId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTokens_Instance_Status",
                table: "WorkflowTokens",
                columns: new[] { "InstanceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinStates");

            migrationBuilder.DropTable(
                name: "WorkflowTokens");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "TokenId",
                table: "WorkflowTasks");
        }
    }
}
