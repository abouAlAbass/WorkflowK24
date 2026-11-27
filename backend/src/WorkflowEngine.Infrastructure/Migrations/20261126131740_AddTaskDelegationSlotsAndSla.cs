using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDelegationSlotsAndSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "WorkflowTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedByUserId",
                table: "WorkflowTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedByUserId",
                table: "WorkflowTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "WorkflowTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "WorkflowTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnBehalfOfUserId",
                table: "WorkflowTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalAssigneeId",
                table: "WorkflowTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalAssigneeType",
                table: "WorkflowTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<int>(
                name: "QuorumCount",
                table: "WorkflowTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionPolicy",
                table: "WorkflowTasks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Single");

            migrationBuilder.CreateTable(
                name: "DelegationRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DelegatorId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DelegateId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "*"),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FromAssigneeId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ToAssigneeId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ToAssigneeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_WorkflowTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "WorkflowTasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssigneeUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OnBehalfOfUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskSlots_WorkflowTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "WorkflowTasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_Status_DueAt",
                table: "WorkflowTasks",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationRules_Delegator_Active_Window",
                table: "DelegationRules",
                columns: new[] { "DelegatorId", "Active", "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_Task_CreatedAt",
                table: "TaskAssignments",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_TaskSlots_Task_Assignee",
                table: "TaskSlots",
                columns: new[] { "TaskId", "AssigneeUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationRules");

            migrationBuilder.DropTable(
                name: "TaskAssignments");

            migrationBuilder.DropTable(
                name: "TaskSlots");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTasks_Status_DueAt",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfUserId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "OriginalAssigneeId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "OriginalAssigneeType",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "QuorumCount",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "ResolutionPolicy",
                table: "WorkflowTasks");
        }
    }
}
