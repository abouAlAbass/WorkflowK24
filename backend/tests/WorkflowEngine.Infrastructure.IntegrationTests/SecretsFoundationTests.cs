using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Security;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Infrastructure.IntegrationTests;

public class SecretsFoundationTests
{
    private static string CreateIsolatedConnectionString(string testPrefix)
    {
        var dbName = $"Tamam_{testPrefix}_{Guid.NewGuid():N}";
        return $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
    }

    private static ServiceProvider BuildInfrastructureProvider(
        string connectionString,
        IDictionary<string, string?>? extraConfig = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionString"] = connectionString
        };

        if (extraConfig is not null)
        {
            foreach (var kvp in extraConfig)
            {
                values[kvp.Key] = kvp.Value;
            }
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        return services.BuildServiceProvider();
    }

    private static async Task<bool> TableExistsAsync(WorkflowEngineDbContext context, string tableName)
    {
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName";
        var param = cmd.CreateParameter();
        param.ParameterName = "@tableName";
        param.Value = tableName;
        cmd.Parameters.Add(param);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    [Fact]
    public async Task SecretProtector_VersionedFormat_EncryptsAndDecryptsWithSamePurpose()
    {
        var connStr = CreateIsolatedConnectionString("SecProtect");
        await using var sp = BuildInfrastructureProvider(connStr);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        try
        {
            await db.Database.MigrateAsync();

            var protector = sp.GetRequiredService<ISecretProtector>();
            const string plaintext = "super-sensitive-oidc-client-secret-2026!";

            // Act
            string protectedPayload = protector.Protect(plaintext, SecretPurposes.AuthProviders);

            // Assert versioned JSON envelope {"v":1,"cipher":"..."}
            using var doc = JsonDocument.Parse(protectedPayload);
            doc.RootElement.GetProperty("v").GetInt32().Should().Be(1);
            string? cipher = doc.RootElement.GetProperty("cipher").GetString();
            cipher.Should().NotBeNullOrWhiteSpace();
            cipher.Should().NotContain(plaintext);

            string decrypted = protector.Unprotect(protectedPayload, SecretPurposes.AuthProviders);
            decrypted.Should().Be(plaintext);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task SecretProtector_WithDifferentPurpose_ThrowsCryptographicException()
    {
        var connStr = CreateIsolatedConnectionString("SecPurpose");
        await using var sp = BuildInfrastructureProvider(connStr);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        try
        {
            await db.Database.MigrateAsync();

            var protector = sp.GetRequiredService<ISecretProtector>();
            string protectedPayload = protector.Protect("db-connection-secret-value", SecretPurposes.Connections);

            // Act & Assert: every other purpose must fail with CryptographicException
            Action unprotectWithAuthPurpose = () => protector.Unprotect(protectedPayload, SecretPurposes.AuthProviders);
            Action unprotectWithUserCredPurpose = () => protector.Unprotect(protectedPayload, SecretPurposes.UserCredentials);
            Action unprotectWithExternalTokenPurpose = () => protector.Unprotect(protectedPayload, SecretPurposes.ExternalTokens);

            unprotectWithAuthPurpose.Should().Throw<CryptographicException>();
            unprotectWithUserCredPurpose.Should().Throw<CryptographicException>();
            unprotectWithExternalTokenPurpose.Should().Throw<CryptographicException>();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task DataProtectionKeyRing_PersistsInDatabase_AndSurvivesServiceProviderRebuild()
    {
        var connStr = CreateIsolatedConnectionString("KeyRingPersist");
        const string originalSecret = "external-oauth-refresh-token-xyz-987654";
        string protectedPayload;

        // First instance: migrate DB, protect secret, verify key persisted to DataProtectionKeys
        await using (var firstProvider = BuildInfrastructureProvider(connStr))
        {
            using var scope = firstProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            await db.Database.MigrateAsync();

            var firstProtector = firstProvider.GetRequiredService<ISecretProtector>();
            protectedPayload = firstProtector.Protect(originalSecret, SecretPurposes.ExternalTokens);

            var persistedKeys = await db.DataProtectionKeys.AsNoTracking().ToListAsync();
            persistedKeys.Should().NotBeEmpty("ASP.NET Core Data Protection must persist its key ring in DataProtectionKeys");
            persistedKeys.Should().AllSatisfy(k => k.Xml.Should().NotBeNullOrWhiteSpace());
        }

        // Second completely fresh ServiceProvider (simulates restart or another container instance)
        await using var secondProvider = BuildInfrastructureProvider(connStr);
        using var secondScope = secondProvider.CreateScope();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        try
        {
            var secondProtector = secondProvider.GetRequiredService<ISecretProtector>();
            string decrypted = secondProtector.Unprotect(protectedPayload, SecretPurposes.ExternalTokens);

            decrypted.Should().Be(originalSecret);
        }
        finally
        {
            await secondDb.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task AuditService_RecordsTypedEventsWithActor_AndStripsOrBlocksSecretValues()
    {
        var connStr = CreateIsolatedConnectionString("AuditGuard");
        await using var sp = BuildInfrastructureProvider(connStr);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        try
        {
            await db.Database.MigrateAsync();

            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var metadataWithSecrets = new Dictionary<string, object?>
            {
                ["connectionName"] = "ERP_Production",
                ["keyVersion"] = 1,
                ["password"] = "LEAK_PASSWORD_123",
                ["clientSecret"] = "LEAK_SECRET_456",
                ["accessToken"] = "LEAK_TOKEN_789",
                ["connectionString"] = "Server=prod;Password=LEAK_CONNSTR;",
                ["authorization"] = "Bearer LEAK_AUTH_HEADER",
                ["nested"] = new Dictionary<string, object?>
                {
                    ["region"] = "eu-west-1",
                    ["api_secret"] = "LEAK_NESTED_SECRET",
                    ["refreshToken"] = "LEAK_NESTED_TOKEN"
                }
            };

            // Act
            var recorded = await auditService.RecordAsync(
                SecurityAuditEventType.SecretCreated,
                actorId: "usr-primary-admin",
                targetType: "Connection",
                targetId: "conn-erp-01",
                metadata: metadataWithSecrets);

            await auditService.RecordAsync(
                SecurityAuditEventType.AuthProviderChanged,
                actorId: "usr-primary-admin",
                targetType: "AuthProvider",
                targetId: "oidc-azure-ad",
                metadata: new Dictionary<string, object?> { ["issuer"] = "https://login.microsoftonline.com/tenant" });

            // Assert persisted event
            var persisted = await db.SecurityAuditEvents.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == recorded.Id);

            persisted.Should().NotBeNull();
            persisted!.EventType.Should().Be(SecurityAuditEventType.SecretCreated);
            persisted.ActorId.Should().Be("usr-primary-admin");
            persisted.TargetType.Should().Be("Connection");
            persisted.TargetId.Should().Be("conn-erp-01");
            persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

            persisted.MetadataJson.Should().NotBeNullOrWhiteSpace();
            persisted.MetadataJson.Should().Contain("ERP_Production");
            persisted.MetadataJson.Should().Contain("eu-west-1");

            // Strict secret-leakage guardrail assertions
            persisted.MetadataJson.Should().NotContain("LEAK_PASSWORD_123");
            persisted.MetadataJson.Should().NotContain("LEAK_SECRET_456");
            persisted.MetadataJson.Should().NotContain("LEAK_TOKEN_789");
            persisted.MetadataJson.Should().NotContain("LEAK_CONNSTR");
            persisted.MetadataJson.Should().NotContain("LEAK_AUTH_HEADER");
            persisted.MetadataJson.Should().NotContain("LEAK_NESTED_SECRET");
            persisted.MetadataJson.Should().NotContain("LEAK_NESTED_TOKEN");

            // Query by typed EventType
            var authEvents = await auditService.GetRecentAsync(10, SecurityAuditEventType.AuthProviderChanged);
            authEvents.Should().ContainSingle(e => e.TargetId == "oidc-azure-ad");

            // Non-JSON raw secret string is rejected
            Func<Task> recordInvalidJson = () => auditService.RecordAsync(
                SecurityAuditEventType.SecretRotated,
                actorId: "usr-primary-admin",
                targetType: "Secret",
                targetId: "sec-1",
                metadataJson: "raw-unstructured-password=12345");

            await recordInvalidJson.Should().ThrowAsync<ArgumentException>();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Migration_AddSecretsFoundationAndAudit_IsReversibleUpAndDown()
    {
        var connStr = CreateIsolatedConnectionString("MigRev");
        await using var sp = BuildInfrastructureProvider(connStr);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        try
        {
            var migrator = db.Database.GetService<IMigrator>();
            var allMigrations = db.Database.GetMigrations().ToList();

            var targetMigration = allMigrations.Single(m => m.EndsWith("_AddSecretsFoundationAndAudit", StringComparison.Ordinal));
            var targetIndex = allMigrations.IndexOf(targetMigration);
            targetIndex.Should().BeGreaterThan(0, "AddSecretsFoundationAndAudit must follow previous migrations");
            var previousMigration = allMigrations[targetIndex - 1];

            // 1. Migrate UP to AddSecretsFoundationAndAudit
            await migrator.MigrateAsync(targetMigration);
            (await TableExistsAsync(db, "DataProtectionKeys")).Should().BeTrue();
            (await TableExistsAsync(db, "SecurityAuditEvents")).Should().BeTrue();

            // 2. Migrate DOWN to previous migration
            await migrator.MigrateAsync(previousMigration);
            (await TableExistsAsync(db, "DataProtectionKeys")).Should().BeFalse();
            (await TableExistsAsync(db, "SecurityAuditEvents")).Should().BeFalse();

            // 3. Migrate UP again to verify clean re-application
            await migrator.MigrateAsync(targetMigration);
            (await TableExistsAsync(db, "DataProtectionKeys")).Should().BeTrue();
            (await TableExistsAsync(db, "SecurityAuditEvents")).Should().BeTrue();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ExternalVaultClient_AndDesignerRole_AreConfiguredCorrectly()
    {
        var connStr = CreateIsolatedConnectionString("VaultDesigner");
        await using var sp = BuildInfrastructureProvider(connStr, new Dictionary<string, string?>
        {
            ["Vault:ErpApiKey"] = "vault-erp-key-value"
        });

        var vaultClient = sp.GetRequiredService<IExternalVaultClient>();
        var secretProvider = sp.GetRequiredService<ISecretProvider>();

        (await vaultClient.GetSecretAsync("ErpApiKey")).Should().Be("vault-erp-key-value");
        (await vaultClient.GetSecretAsync("NonExistentKey")).Should().BeNull();
        (await secretProvider.ResolveSecretAsync("vault:ErpApiKey")).Should().Be("vault-erp-key-value");

        // Verify Designer role & permissions
        Roles.IsValid(Roles.Designer).Should().BeTrue();
        Roles.Assignable.Should().Contain(Roles.Designer);
        Permissions.RoleHas(Roles.Designer, Permissions.WorkflowsWrite).Should().BeTrue();
        Permissions.RoleHas(Roles.Designer, Permissions.DesignsWrite).Should().BeTrue();
        Permissions.RoleHas(Roles.Designer, Permissions.UsersManage).Should().BeFalse();
    }
}
