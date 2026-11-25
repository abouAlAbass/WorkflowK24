using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Infrastructure.Persistence;

/// <summary>
/// Automatically applies pending EF Core migrations (SQL Server) or initializes relational schema
/// and seeds initial enterprise users and settings.
/// </summary>
public static class DatabaseInitializer
{
    public const string DefaultSeedPassword = "Password123!";

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WorkflowEngineDbContext>>();

        try
        {
            if (context.Database.IsRelational())
            {
                if (context.Database.IsSqlServer())
                {
                    var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        logger.LogInformation("Applying {Count} pending EF Core migration(s)...", pendingMigrations.Count);
                        await context.Database.MigrateAsync();
                        logger.LogInformation("EF Core migrations applied successfully.");
                    }
                    else
                    {
                        await context.Database.MigrateAsync();
                    }
                }
                else
                {
                    await context.Database.EnsureCreatedAsync();
                    logger.LogInformation("Database schema verified and created successfully.");
                }

                await SeedDefaultUsersAsync(context, logger);
                await SeedDefaultSettingsAsync(context, logger);
                await SeedDefaultSmartFormsAsync(context, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while automatically migrating or initializing the database.");
            throw;
        }
    }

    private static async Task SeedDefaultUsersAsync(WorkflowEngineDbContext context, ILogger logger)
    {
        string defaultHash = PasswordHasher.HashPassword(DefaultSeedPassword);

        var seedUsers = new[]
        {
            new SystemUser("usr-1", "Adel Sebti", "a.sebti@enterprise.internal", "Administrator", "IT & Security", "Active", "/avatars/user-2.svg", defaultHash),
            new SystemUser("usr-2", "Karim Khmissi", "k.khmissi@enterprise.internal", "Workflow Designer", "Operations", "Active", "/avatars/user-3.svg", defaultHash),
            new SystemUser("usr-3", "Mohamed Fetni", "m.fetni@enterprise.internal", "Process Manager", "Finance", "Active", "/avatars/user-5.svg", defaultHash),
            new SystemUser("usr-4", "Yacine Benali", "y.benali@enterprise.internal", "End User", "Legal", "Inactive", "/avatars/user-1.svg", defaultHash),
            new SystemUser("usr-5", "Adel Sebti", "adel.sebti@corp.internal", "BPM Administrator", "IT & Security", "Active", "/avatars/user-2.svg", defaultHash),
            new SystemUser("usr-6", "Karim Khmissi", "karim.khmissi@corp.internal", "Process Manager", "Finance Managers", "Active", "/avatars/user-3.svg", defaultHash)
        };

        var existingEmails = await context.SystemUsers
            .Select(u => u.Email)
            .ToListAsync();

        var existingSet = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);
        bool hasChanges = false;

        foreach (var seed in seedUsers)
        {
            if (!existingSet.Contains(seed.Email))
            {
                context.SystemUsers.Add(seed);
                hasChanges = true;
            }
        }

        // Upgrade any pre-existing seeded user whose PasswordHash was empty prior to P0 security migration
        var unhashedUsers = await context.SystemUsers
            .Where(u => u.PasswordHash == "")
            .ToListAsync();

        foreach (var legacyUser in unhashedUsers)
        {
            legacyUser.SetPassword(DefaultSeedPassword);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded/updated default SystemUsers with PBKDF2 credentials.");
        }
    }

    private static async Task SeedDefaultSettingsAsync(WorkflowEngineDbContext context, ILogger logger)
    {
        if (!await context.SystemSettings.AnyAsync())
        {
            context.SystemSettings.AddRange(
                new SystemSetting("smtp.server", "smtp.enterprise.internal", "Email"),
                new SystemSetting("smtp.port", "587", "Email"),
                new SystemSetting("system.retention_days", "90", "System"),
                new SystemSetting("telemetry.enabled", "true", "Monitoring"),
                new SystemSetting("security.mfa_enforced", "false", "Security")
            );
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default SystemSettings.");
        }
    }

    private static async Task SeedDefaultSmartFormsAsync(WorkflowEngineDbContext context, ILogger logger)
    {
        if (!await context.SmartForms.AnyAsync())
        {
            const string purchaseFieldsJson = """
            [
              {"id":"f1","name":"VendorName","label":"Vendor Name","type":"text","required":true,"readOnly":false,"width":"half","placeholder":"e.g. Dell Technologies"},
              {"id":"f2","name":"Department","label":"Cost Center / Department","type":"select","required":true,"readOnly":false,"width":"half","options":["Engineering","Finance","IT Security","Operations","HR"]},
              {"id":"f3","name":"Amount","label":"Total Purchase Amount ($)","type":"currency","required":true,"readOnly":false,"width":"half","defaultValue":"4500"},
              {"id":"f4","name":"NeededByDate","label":"Required Delivery Date","type":"date","required":true,"readOnly":false,"width":"half"},
              {"id":"f5","name":"ExecutiveJustification","label":"Executive Justification (> $5,000)","type":"textarea","required":false,"readOnly":false,"width":"full","placeholder":"Explain ROI for high-value purchase...","visibilityRule":{"field":"Amount","operator":"greater_than","value":"5000"}},
              {"id":"f6","name":"LineItems","label":"Order Line Items","type":"repeater","required":false,"readOnly":false,"width":"full","repeaterColumns":[{"name":"Item","label":"Item Description","type":"text","required":true},{"name":"Qty","label":"Qty","type":"number","required":true},{"name":"UnitPrice","label":"Unit Price ($)","type":"number","required":true}]},
              {"id":"f7","name":"QuoteAttachment","label":"Vendor Quote (PDF / Image)","type":"file","required":false,"readOnly":false,"width":"full"}
            ]
            """;

            context.SmartForms.Add(new SmartFormEntity(
                "form-purchase-req",
                "PurchaseRequisitionForm",
                "Executive Purchase Requisition",
                "No-Code procurement request form with conditional justification, line-item repeater, and vendor quote attachment.",
                "Finance",
                purchaseFieldsJson,
                "so-expense",
                "expense",
                "Published",
                publishImmediately: true,
                publishedBy: "system"));

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default SmartForms.");
        }

        // Backfill any pre-existing SmartForms that have no SmartFormVersions rows yet
        var legacyForms = await context.SmartForms
            .Include(f => f.Versions)
            .Where(f => !f.Versions.Any())
            .ToListAsync();

        if (legacyForms.Count > 0)
        {
            foreach (var form in legacyForms)
            {
                var v1 = form.EnsureInitialPublishedVersion("system");
                context.SmartFormVersions.Add(v1);
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Backfilled initial Published SmartFormVersion v1 for {Count} existing SmartForm(s).", legacyForms.Count);
        }
    }
}
