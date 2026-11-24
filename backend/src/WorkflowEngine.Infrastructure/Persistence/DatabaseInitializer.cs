using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkflowEngine.Infrastructure.Persistence;

/// <summary>
/// Helper to automatically apply pending EF Core migrations or ensure schema creation on startup.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WorkflowEngineDbContext>>();

        try
        {
            if (context.Database.IsRelational())
            {
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Applying {Count} pending EF Core migration(s)...", pendingMigrations.Count());
                    await context.Database.MigrateAsync();
                    logger.LogInformation("EF Core migrations applied successfully.");
                }
                else
                {
                    // Ensures all tables, constraints, and indexes from schema.sql are created immediately
                    await context.Database.EnsureCreatedAsync();
                    logger.LogInformation("Database verified and tables created successfully.");
                }

                // Ensure SmartObjects and SmartObjectRecords tables exist in existing databases
                string ddl = context.Database.IsSqlServer()
                    ? """
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SmartObjects')
                        BEGIN
                            CREATE TABLE [SmartObjects] (
                                [Id] NVARCHAR(100) NOT NULL,
                                [Name] NVARCHAR(150) NOT NULL,
                                [DisplayName] NVARCHAR(255) NOT NULL,
                                [Description] NVARCHAR(1000) NULL,
                                [Category] NVARCHAR(100) NOT NULL DEFAULT 'Custom',
                                [RecordsCount] INT NOT NULL DEFAULT 0,
                                [FieldsJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                                [CreatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                [UpdatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                CONSTRAINT [PK_SmartObjects] PRIMARY KEY ([Id]),
                                CONSTRAINT [UQ_SmartObjects_Name] UNIQUE ([Name])
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SmartObjectRecords')
                        BEGIN
                            CREATE TABLE [SmartObjectRecords] (
                                [Id] BIGINT IDENTITY(1,1) NOT NULL,
                                [SmartObjectId] NVARCHAR(100) NOT NULL,
                                [RecordKey] NVARCHAR(150) NOT NULL,
                                [DataJson] NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                                [CreatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                [UpdatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                CONSTRAINT [PK_SmartObjectRecords] PRIMARY KEY ([Id]),
                                CONSTRAINT [UQ_SmartObjectRecords_Object_Key] UNIQUE ([SmartObjectId], [RecordKey])
                            );
                        END;
                    """
                    : """
                        CREATE TABLE IF NOT EXISTS "SmartObjects" (
                            "Id" VARCHAR(100) PRIMARY KEY,
                            "Name" VARCHAR(150) UNIQUE NOT NULL,
                            "DisplayName" VARCHAR(255) NOT NULL,
                            "Description" VARCHAR(1000) NULL,
                            "Category" VARCHAR(100) NOT NULL DEFAULT 'Custom',
                            "RecordsCount" INT NOT NULL DEFAULT 0,
                            "FieldsJson" TEXT NOT NULL DEFAULT '[]',
                            "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE TABLE IF NOT EXISTS "SmartObjectRecords" (
                            "Id" BIGSERIAL PRIMARY KEY,
                            "SmartObjectId" VARCHAR(100) NOT NULL,
                            "RecordKey" VARCHAR(150) NOT NULL,
                            "DataJson" TEXT NOT NULL DEFAULT '{}',
                            "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            CONSTRAINT "UQ_SmartObjectRecords_Object_Key" UNIQUE ("SmartObjectId", "RecordKey")
                        );

                        CREATE TABLE IF NOT EXISTS "SystemUsers" (
                            "Id" VARCHAR(100) PRIMARY KEY,
                            "Name" VARCHAR(150) NOT NULL,
                            "Email" VARCHAR(255) UNIQUE NOT NULL,
                            "Role" VARCHAR(100) NOT NULL DEFAULT 'End User',
                            "Group" VARCHAR(100) NOT NULL DEFAULT 'General',
                            "Status" VARCHAR(50) NOT NULL DEFAULT 'Active',
                            "AvatarUrl" VARCHAR(500) NOT NULL DEFAULT '',
                            "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE TABLE IF NOT EXISTS "SystemSettings" (
                            "Key" VARCHAR(100) PRIMARY KEY,
                            "Value" TEXT NOT NULL DEFAULT '',
                            "Category" VARCHAR(100) NOT NULL DEFAULT 'General',
                            "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                        );
                    """;

                if (context.Database.IsSqlServer())
                {
                    ddl += """

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemUsers')
                        BEGIN
                            CREATE TABLE [SystemUsers] (
                                [Id] NVARCHAR(100) NOT NULL,
                                [Name] NVARCHAR(150) NOT NULL,
                                [Email] NVARCHAR(255) NOT NULL,
                                [Role] NVARCHAR(100) NOT NULL DEFAULT 'End User',
                                [Group] NVARCHAR(100) NOT NULL DEFAULT 'General',
                                [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
                                [AvatarUrl] NVARCHAR(500) NOT NULL DEFAULT '',
                                [CreatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                [UpdatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                CONSTRAINT [PK_SystemUsers] PRIMARY KEY ([Id]),
                                CONSTRAINT [UQ_SystemUsers_Email] UNIQUE ([Email])
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
                        BEGIN
                            CREATE TABLE [SystemSettings] (
                                [Key] NVARCHAR(100) NOT NULL,
                                [Value] NVARCHAR(MAX) NOT NULL DEFAULT '',
                                [Category] NVARCHAR(100) NOT NULL DEFAULT 'General',
                                [UpdatedAt] DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Key])
                            );
                        END;
                    """;
                }

                var connection = context.Database.GetDbConnection();
                await context.Database.OpenConnectionAsync();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = ddl;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Seed initial system users if empty
                if (!await context.SystemUsers.AnyAsync())
                {
                    context.SystemUsers.AddRange(
                        new Domain.Entities.SystemUser("usr-1", "Adel Sebti", "a.sebti@enterprise.internal", "Administrator", "IT & Security", "Active", "/avatars/user-2.svg"),
                        new Domain.Entities.SystemUser("usr-2", "Karim Khmissi", "k.khmissi@enterprise.internal", "Workflow Designer", "Operations", "Active", "/avatars/user-3.svg"),
                        new Domain.Entities.SystemUser("usr-3", "Mohamed Fetni", "m.fetni@enterprise.internal", "Task Approver", "Finance", "Active", "/avatars/user-5.svg"),
                        new Domain.Entities.SystemUser("usr-4", "Yacine Benali", "y.benali@enterprise.internal", "End User", "Legal", "Inactive", "/avatars/user-1.svg")
                    );
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded default SystemUsers.");
                }

                // Seed default settings if empty
                if (!await context.SystemSettings.AnyAsync())
                {
                    context.SystemSettings.AddRange(
                        new Domain.Entities.SystemSetting("smtp.server", "smtp.enterprise.internal", "Email"),
                        new Domain.Entities.SystemSetting("smtp.port", "587", "Email"),
                        new Domain.Entities.SystemSetting("system.retention_days", "90", "System"),
                        new Domain.Entities.SystemSetting("telemetry.enabled", "true", "Monitoring"),
                        new Domain.Entities.SystemSetting("security.mfa_enforced", "false", "Security")
                    );
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded default SystemSettings.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while automatically migrating or initializing the database.");
            throw;
        }
    }
}
