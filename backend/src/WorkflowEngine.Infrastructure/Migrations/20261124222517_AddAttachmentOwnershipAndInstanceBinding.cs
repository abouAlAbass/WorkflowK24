using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentOwnershipAndInstanceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploadedBy",
                table: "FormAttachments",
                newName: "UploadedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "FormAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowInstanceId",
                table: "FormAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormAttachments_UploadedByUserId",
                table: "FormAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAttachments_WorkflowInstanceId",
                table: "FormAttachments",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormAttachments_UploadedByUserId",
                table: "FormAttachments");

            migrationBuilder.DropIndex(
                name: "IX_FormAttachments_WorkflowInstanceId",
                table: "FormAttachments");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "FormAttachments");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "FormAttachments");

            migrationBuilder.RenameColumn(
                name: "UploadedByUserId",
                table: "FormAttachments",
                newName: "UploadedBy");
        }
    }
}
