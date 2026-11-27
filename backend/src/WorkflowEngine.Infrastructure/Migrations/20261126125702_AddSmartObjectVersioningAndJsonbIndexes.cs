using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartObjectVersioningAndJsonbIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_SmartObjects_Name",
                table: "SmartObjects");

            migrationBuilder.AddColumn<string>(
                name: "MethodsJson",
                table: "SmartObjects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "SmartObjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedBy",
                table: "SmartObjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SmartObjects",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SmartObjects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "SmartObjectRecordId",
                table: "SmartFormSubmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmartObjects_Name",
                table: "SmartObjects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_SmartObjects_Name_Version",
                table: "SmartObjects",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmartFormSubmissions_SmartObjectRecordId",
                table: "SmartFormSubmissions",
                column: "SmartObjectRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmartFormSubmissions_SmartObjectRecords_SmartObjectRecordId",
                table: "SmartFormSubmissions",
                column: "SmartObjectRecordId",
                principalTable: "SmartObjectRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    ALTER TABLE "SmartObjects" ALTER COLUMN "FieldsJson" TYPE jsonb USING "FieldsJson"::jsonb;
                    ALTER TABLE "SmartObjects" ALTER COLUMN "MethodsJson" TYPE jsonb USING "MethodsJson"::jsonb;
                    ALTER TABLE "SmartObjectRecords" ALTER COLUMN "DataJson" TYPE jsonb USING "DataJson"::jsonb;
                    ALTER TABLE "SmartFormSubmissions" ALTER COLUMN "DataJson" TYPE jsonb USING "DataJson"::jsonb;
                    CREATE INDEX IF NOT EXISTS "IX_SmartObjectRecords_DataJson_Gin" ON "SmartObjectRecords" USING gin ("DataJson");
                    CREATE INDEX IF NOT EXISTS "IX_SmartFormSubmissions_DataJson_Gin" ON "SmartFormSubmissions" USING gin ("DataJson");
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    DROP INDEX IF EXISTS "IX_SmartFormSubmissions_DataJson_Gin";
                    DROP INDEX IF EXISTS "IX_SmartObjectRecords_DataJson_Gin";
                    ALTER TABLE "SmartFormSubmissions" ALTER COLUMN "DataJson" TYPE text USING "DataJson"::text;
                    ALTER TABLE "SmartObjectRecords" ALTER COLUMN "DataJson" TYPE text USING "DataJson"::text;
                    ALTER TABLE "SmartObjects" ALTER COLUMN "MethodsJson" TYPE text USING "MethodsJson"::text;
                    ALTER TABLE "SmartObjects" ALTER COLUMN "FieldsJson" TYPE text USING "FieldsJson"::text;
                    """);
            }

            migrationBuilder.DropForeignKey(
                name: "FK_SmartFormSubmissions_SmartObjectRecords_SmartObjectRecordId",
                table: "SmartFormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_SmartObjects_Name",
                table: "SmartObjects");

            migrationBuilder.DropIndex(
                name: "UQ_SmartObjects_Name_Version",
                table: "SmartObjects");

            migrationBuilder.DropIndex(
                name: "IX_SmartFormSubmissions_SmartObjectRecordId",
                table: "SmartFormSubmissions");

            migrationBuilder.DropColumn(
                name: "MethodsJson",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SmartObjects");

            migrationBuilder.DropColumn(
                name: "SmartObjectRecordId",
                table: "SmartFormSubmissions");

            migrationBuilder.CreateIndex(
                name: "UQ_SmartObjects_Name",
                table: "SmartObjects",
                column: "Name",
                unique: true);
        }
    }
}
