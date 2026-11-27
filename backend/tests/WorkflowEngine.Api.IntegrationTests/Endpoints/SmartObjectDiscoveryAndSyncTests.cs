using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Services.Brokers;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

#region Configurable Discovery & Query Fake for SQL Server Broker

public sealed record FakeSchemaColumn(
    string SchemaName,
    string EntityName,
    string EntityType,
    string ColumnName,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey);

public sealed class DiscoveryFakeSqlServerCommandRunner : ISqlServerCommandRunner
{
    private readonly object _lock = new();
    public List<FakeSchemaColumn> SchemaColumns { get; } = new();
    public List<Dictionary<string, object?>> DataRows { get; } = new();
    public ConcurrentQueue<string> ExecutedQueries { get; } = new();

    public DiscoveryFakeSqlServerCommandRunner()
    {
        ResetDefaultSchema();
    }

    public void ResetDefaultSchema()
    {
        lock (_lock)
        {
            SchemaColumns.Clear();
            DataRows.Clear();

            // Table 1: dbo.employees (with PK, known types, unknown SQL types, and sensitive columns)
            SchemaColumns.AddRange(new[]
            {
                new FakeSchemaColumn("dbo", "employees", "Table", "id", "int", false, true),
                new FakeSchemaColumn("dbo", "employees", "Table", "full_name", "nvarchar", false, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "is_active", "bit", false, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "hired_at", "datetime2", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "metadata", "jsonb", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "geo_location", "geography", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "salary", "decimal", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "password_hash", "varchar", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "confidential_code", "varchar", true, false),
                new FakeSchemaColumn("dbo", "employees", "Table", "internal_notes", "text", true, false)
            });

            // Table 2: dbo.audit_view (View with NO primary key)
            SchemaColumns.AddRange(new[]
            {
                new FakeSchemaColumn("dbo", "audit_view", "View", "event_name", "varchar", false, false),
                new FakeSchemaColumn("dbo", "audit_view", "View", "event_count", "bigint", false, false),
                new FakeSchemaColumn("dbo", "audit_view", "View", "occurred_at", "datetime", true, false)
            });

            // Table 3: dbo.sync_items (used by SyncSchema test)
            SchemaColumns.AddRange(new[]
            {
                new FakeSchemaColumn("dbo", "sync_items", "Table", "item_id", "int", false, true),
                new FakeSchemaColumn("dbo", "sync_items", "Table", "title", "nvarchar", false, false),
                new FakeSchemaColumn("dbo", "sync_items", "Table", "rating", "varchar", true, false),
                new FakeSchemaColumn("dbo", "sync_items", "Table", "legacy_flag", "bit", true, false)
            });

            DataRows.Add(new Dictionary<string, object?>
            {
                ["id"] = 42,
                ["full_name"] = "Sami Benali",
                ["confidential_code"] = "ULTRA-CONFIDENTIAL-7788",
                ["salary"] = 145000m
            });
        }
    }

    public void MutateSyncItemsSchema()
    {
        lock (_lock)
        {
            SchemaColumns.RemoveAll(c => string.Equals(c.EntityName, "sync_items", StringComparison.OrdinalIgnoreCase));
            SchemaColumns.AddRange(new[]
            {
                // PK changed from item_id -> item_uuid
                new FakeSchemaColumn("dbo", "sync_items", "Table", "item_id", "int", false, false),
                new FakeSchemaColumn("dbo", "sync_items", "Table", "item_uuid", "varchar", false, true),
                new FakeSchemaColumn("dbo", "sync_items", "Table", "title", "nvarchar", false, false),
                // Type changed: varchar ("string") -> int ("number")
                new FakeSchemaColumn("dbo", "sync_items", "Table", "rating", "int", true, false),
                // Added column
                new FakeSchemaColumn("dbo", "sync_items", "Table", "department_code", "nvarchar", true, false)
                // Removed column: legacy_flag
            });
        }
    }

    public Task<object?> ExecuteScalarAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        ExecutedQueries.Enqueue(commandText);
        return Task.FromResult<object?>(1);
    }

    public Task<SqlServerQueryResult> ExecuteQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        int maxResponseBytes,
        CancellationToken ct = default)
    {
        ExecutedQueries.Enqueue(commandText);

        lock (_lock)
        {
            // 1. Full schema discovery query (sys.tables + sys.views + sys.index_columns)
            if (commandText.Contains("sys.index_columns", StringComparison.OrdinalIgnoreCase))
            {
                var rows = SchemaColumns.Select(c => new Dictionary<string, object?>
                {
                    ["schema_name"] = c.SchemaName,
                    ["entity_name"] = c.EntityName,
                    ["entity_type"] = c.EntityType,
                    ["column_name"] = c.ColumnName,
                    ["data_type"] = c.DataType,
                    ["is_nullable"] = c.IsNullable ? 1 : 0,
                    ["is_primary_key"] = c.IsPrimaryKey ? 1 : 0
                }).ToList();

                return Task.FromResult(new SqlServerQueryResult(rows, rows.Count));
            }

            // 2. Column whitelist validation for InvokeAsync
            if (commandText.Contains("sys.columns", StringComparison.OrdinalIgnoreCase))
            {
                string schema = parameters.FirstOrDefault(p => p.ParameterName == "@schema")?.Value?.ToString() ?? "dbo";
                string table = parameters.FirstOrDefault(p => p.ParameterName == "@table")?.Value?.ToString() ?? string.Empty;

                var cols = SchemaColumns
                    .Where(c => string.Equals(c.SchemaName, schema, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.EntityName, table, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new Dictionary<string, object?> { ["column_name"] = c.ColumnName })
                    .ToList();

                return Task.FromResult(new SqlServerQueryResult(cols, cols.Count));
            }

            // 3. Data SELECT queries
            return Task.FromResult(new SqlServerQueryResult(DataRows.ToList(), DataRows.Count));
        }
    }

    public Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        ExecutedQueries.Enqueue(commandText);
        return Task.FromResult(1);
    }
}

public class SmartObjectDiscoveryTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineDiscoveryTestDb_{Guid.NewGuid():N}";

    public DiscoveryFakeSqlServerCommandRunner SqlServerRunner { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");
        builder.UseSetting("Seed:PrimaryAdmin:Email", "adel.sebti@corp.internal");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISqlServerCommandRunner>();
            services.AddSingleton<ISqlServerCommandRunner>(SqlServerRunner);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }

        await base.DisposeAsync();
    }
}

#endregion

/// <summary>
/// Étape E.2c — Schema Discovery, Selective SmartObject Generation, Primary Key CRUD Guard,
/// Schema Diff &amp; Synchronization, and Runtime Field Masking in ViewFlow &amp; InstanceEvents.
/// </summary>
public class SmartObjectDiscoveryAndSyncTests : IClassFixture<SmartObjectDiscoveryTestFactory>
{
    private readonly SmartObjectDiscoveryTestFactory _factory;

    public SmartObjectDiscoveryAndSyncTests(SmartObjectDiscoveryTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "adel.sebti@corp.internal",
        string password = DatabaseInitializer.DefaultSeedPassword)
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private async Task<ConnectionDto> CreateSqlServerConnectionAsync(HttpClient client, bool allowWrite)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var res = await client.PostAsJsonAsync("/api/connections", new
        {
            name = $"sql-discovery-{suffix}",
            serviceType = "sqlserver",
            baseUrl = "sqlserver://sql-prod.internal:1433/hr_db",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            credentialsJson = """{"server":"sql-prod.internal","database":"hr_db","username":"svc_bpm","password":"TestPassword!"}""",
            allowWrite,
            maxRows = 200
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<ConnectionDto>();
        dto.Should().NotBeNull();
        return dto!;
    }

    [Fact]
    public async Task DiscoverAndGenerate_ExposesOnlySelectedFields_AndMapsUnknownTypesToString()
    {
        _factory.SqlServerRunner.ResetDefaultSchema();
        var client = await CreateAuthenticatedClientAsync();
        var conn = await CreateSqlServerConnectionAsync(client, allowWrite: true);

        // 1. Discover entities via GET /api/connections/{id}/discover
        var discoverRes = await client.GetAsync($"/api/connections/{conn.Id}/discover");
        discoverRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var entities = await discoverRes.Content.ReadFromJsonAsync<List<DiscoveredEntityDto>>();
        entities.Should().NotBeNull();
        var employees = entities!.FirstOrDefault(e => e.EntityName == "employees");
        employees.Should().NotBeNull();
        employees!.DetectedPrimaryKey.Should().Be("id");

        // Verify documented type mappings & fallback to "string" for unknown types (geography)
        employees.Fields.First(f => f.Name == "id").MappedSmartObjectType.Should().Be("number");
        employees.Fields.First(f => f.Name == "is_active").MappedSmartObjectType.Should().Be("boolean");
        employees.Fields.First(f => f.Name == "hired_at").MappedSmartObjectType.Should().Be("datetime");
        employees.Fields.First(f => f.Name == "metadata").MappedSmartObjectType.Should().Be("json");
        employees.Fields.First(f => f.Name == "geo_location").MappedSmartObjectType.Should().Be("string");

        // Verify suggestedMasked on sensitive columns
        employees.Fields.First(f => f.Name == "salary").SuggestedMasked.Should().BeTrue();
        employees.Fields.First(f => f.Name == "password_hash").SuggestedMasked.Should().BeTrue();
        employees.Fields.First(f => f.Name == "full_name").SuggestedMasked.Should().BeFalse();

        // 2. Generate SmartObject exposing ONLY 3 selected fields (excluding internal_notes & password_hash)
        string soKey = $"EmployeePublic_{Guid.NewGuid():N}"[..24];
        var genRes = await client.PostAsJsonAsync($"/api/connections/{conn.Id}/generate-smartobject", new
        {
            entityName = "employees",
            smartObjectKey = soKey,
            displayName = "Employee Directory",
            primaryKeyField = "id",
            selectedFields = new object[]
            {
                new { name = "id", displayName = "ID", type = "int", masked = false },
                new { name = "full_name", displayName = "Full Name", type = "nvarchar", masked = false },
                new { name = "geo_location", displayName = "Location", type = "geography", masked = false }
            }
        });
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var generated = await genRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        generated.Should().NotBeNull();
        generated!.Fields.Should().HaveCount(3);
        generated.Fields.Select(f => f.Name).Should().BeEquivalentTo(new[] { "id", "full_name", "geo_location" });
        generated.Fields.Should().NotContain(f => f.Name == "internal_notes" || f.Name == "password_hash");
        generated.Fields.First(f => f.Name == "geo_location").Type.Should().Be("string");
        generated.Fields.First(f => f.Name == "id").Type.Should().Be("number");
        generated.Fields.First(f => f.Name == "id").IsPrimaryKey.Should().BeTrue();

        // AllowWrite = true + PrimaryKey set -> all 5 CRUD methods generated
        generated.Methods!.Select(m => m.Name).Should().BeEquivalentTo(new[] { "Get", "List", "Create", "Update", "Delete" });
    }

    [Fact]
    public async Task GenerateSmartObject_WithoutPrimaryKey_DisablesUpdateAndDeleteMethods()
    {
        _factory.SqlServerRunner.ResetDefaultSchema();
        var client = await CreateAuthenticatedClientAsync();
        var writableConn = await CreateSqlServerConnectionAsync(client, allowWrite: true);
        var readOnlyConn = await CreateSqlServerConnectionAsync(client, allowWrite: false);

        // 1. Discover audit_view and confirm DetectedPrimaryKey is null
        var discoverRes = await client.GetAsync($"/api/connections/{writableConn.Id}/discover");
        discoverRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var entities = await discoverRes.Content.ReadFromJsonAsync<List<DiscoveredEntityDto>>();
        var auditView = entities!.First(e => e.EntityName == "audit_view");
        auditView.DetectedPrimaryKey.Should().BeNull();

        // 2. Generate SmartObject with AllowWrite = true but NO primaryKeyField
        string soKey = $"AuditView_{Guid.NewGuid():N}"[..22];
        var genRes = await client.PostAsJsonAsync($"/api/connections/{writableConn.Id}/generate-smartobject", new
        {
            entityName = "audit_view",
            smartObjectKey = soKey,
            displayName = "Audit View",
            primaryKeyField = (string?)null,
            selectedFields = new object[]
            {
                new { name = "event_name", displayName = "Event Name", type = "string", masked = false },
                new { name = "event_count", displayName = "Count", type = "number", masked = false }
            }
        });
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var generated = await genRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        generated.Should().NotBeNull();
        var methodNames = generated!.Methods!.Select(m => m.Name).ToList();
        methodNames.Should().Contain(new[] { "Get", "List", "Create" });
        methodNames.Should().NotContain("Update");
        methodNames.Should().NotContain("Delete");

        // 3. Also verify AllowWrite = false generates ONLY Get and List even with a PK
        string roKey = $"ReadOnlyEmp_{Guid.NewGuid():N}"[..22];
        var roRes = await client.PostAsJsonAsync($"/api/connections/{readOnlyConn.Id}/generate-smartobject", new
        {
            entityName = "employees",
            smartObjectKey = roKey,
            displayName = "ReadOnly Employees",
            primaryKeyField = "id",
            selectedFields = new object[]
            {
                new { name = "id", displayName = "ID", type = "number", masked = false },
                new { name = "full_name", displayName = "Full Name", type = "string", masked = false }
            }
        });
        roRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var roSo = await roRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        roSo!.Methods!.Select(m => m.Name).Should().BeEquivalentTo(new[] { "Get", "List" });
    }

    [Fact]
    public async Task SyncSchema_DetectsAddedRemovedAndChangedColumns_AndAppliesDiffWhenRequested()
    {
        _factory.SqlServerRunner.ResetDefaultSchema();
        var client = await CreateAuthenticatedClientAsync();
        var conn = await CreateSqlServerConnectionAsync(client, allowWrite: true);

        // 1. Generate SmartObject from initial dbo.sync_items schema
        string soKey = $"SyncItems_{Guid.NewGuid():N}"[..22];
        var genRes = await client.PostAsJsonAsync($"/api/connections/{conn.Id}/generate-smartobject", new
        {
            entityName = "sync_items",
            smartObjectKey = soKey,
            displayName = "Sync Items",
            primaryKeyField = "item_id",
            selectedFields = new object[]
            {
                new { name = "item_id", displayName = "Item ID", type = "number", masked = false },
                new { name = "title", displayName = "Title", type = "string", masked = false },
                new { name = "rating", displayName = "Rating", type = "string", masked = false },
                new { name = "legacy_flag", displayName = "Legacy Flag", type = "boolean", masked = false }
            }
        });
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var generated = await genRes.Content.ReadFromJsonAsync<SmartObjectDto>();

        // 2. Mutate remote schema: add item_uuid + department_code, remove legacy_flag, change rating string->number, change PK item_id->item_uuid
        _factory.SqlServerRunner.MutateSyncItemsSchema();

        // 3. Preview diff (apply = false)
        var previewRes = await client.PostAsJsonAsync($"/api/smartobjects/{generated!.Id}/sync-schema", new { apply = false });
        previewRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var previewDiff = await previewRes.Content.ReadFromJsonAsync<SchemaDiffDto>();
        previewDiff.Should().NotBeNull();
        previewDiff!.Applied.Should().BeFalse();
        previewDiff.HasChanges.Should().BeTrue();
        previewDiff.AddedFields.Select(f => f.Name).Should().Contain(new[] { "item_uuid", "department_code" });
        previewDiff.RemovedFields.Select(f => f.Name).Should().Contain("legacy_flag");
        previewDiff.TypeChangedFields.Should().ContainSingle(c =>
            c.Name == "rating" && c.PreviousType == "string" && c.NewType == "number");
        previewDiff.PrimaryKeyChanged.Should().BeTrue();
        previewDiff.PreviousPrimaryKey.Should().Be("item_id");
        previewDiff.NewPrimaryKey.Should().Be("item_uuid");

        // 4. Apply diff (apply = true)
        var applyRes = await client.PostAsJsonAsync($"/api/smartobjects/{generated.Id}/sync-schema", new { apply = true });
        applyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var appliedDiff = await applyRes.Content.ReadFromJsonAsync<SchemaDiffDto>();
        appliedDiff!.Applied.Should().BeTrue();

        // 5. Verify SmartObject definition was updated & audit event recorded
        var listRes = await client.GetFromJsonAsync<List<SmartObjectDto>>("/api/smartobjects");
        var updatedSo = listRes!.First(s => s.Id == generated.Id);
        updatedSo.Fields.Select(f => f.Name).Should().Contain("department_code");
        updatedSo.Fields.Select(f => f.Name).Should().NotContain("legacy_flag");
        updatedSo.Fields.First(f => f.Name == "rating").Type.Should().Be("number");
        updatedSo.PrimaryKeyField.Should().Be("item_uuid");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        bool audited = await db.SecurityAuditEvents.AnyAsync(e =>
            e.EventType == SecurityAuditEventType.SmartObjectSchemaSynced &&
            e.TargetId == generated.Id);
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task MaskedSmartObjectField_IsRedactedInViewFlowAndEventsForStandardUser()
    {
        _factory.SqlServerRunner.ResetDefaultSchema();
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var standardClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var conn = await CreateSqlServerConnectionAsync(adminClient, allowWrite: false);

        // 1. Generate & publish SmartObject with masked: true on confidential_code (non-default keyword) & salary
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string soKey = $"MaskedEmp_{suffix}";
        var genRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/generate-smartobject", new
        {
            entityName = "employees",
            smartObjectKey = soKey,
            displayName = "Masked Employee",
            primaryKeyField = "id",
            publishImmediately = true,
            selectedFields = new object[]
            {
                new { name = "id", displayName = "ID", type = "number", masked = false },
                new { name = "full_name", displayName = "Full Name", type = "string", masked = false },
                new { name = "confidential_code", displayName = "Confidential Code", type = "string", masked = true },
                new { name = "salary", displayName = "Salary", type = "number", masked = true }
            }
        });
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var so = await genRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        so!.Status.Should().Be("Published");
        so.Fields.First(f => f.Name == "confidential_code").Masked.Should().BeTrue();

        // 2. Publish workflow invoking the generated SmartObject's Get method and mapping confidential_code -> employeeSecretCode
        string wfKey = $"wf-masked-so-{suffix}";
        string graphJson = $$"""
        {
          "nodes": [
            { "id": "start-1", "type": "trigger", "data": { "nodeType": "trigger", "label": "Start", "triggerData": { "triggerType": "form", "eventName": "start", "variables": [] } } },
            {
              "id": "so-1",
              "type": "smartObject",
              "data": {
                "nodeType": "smartObject",
                "label": "Fetch Employee",
                "smartObjectData": {
                  "smartObjectRef": "{{so.ObjectRef}}",
                  "methodName": "Get",
                  "inputMapping": { "id": 42 },
                  "outputMapping": {
                    "employeeName": "full_name",
                    "employeeSecretCode": "confidential_code",
                    "employeeComp": "salary"
                  }
                }
              }
            },
            { "id": "end-1", "type": "end", "data": { "nodeType": "end", "label": "End", "endData": { "status": "completed", "message": "Done" } } }
          ],
          "edges": [
            { "id": "e1", "source": "start-1", "target": "so-1" },
            { "id": "e2", "source": "so-1", "target": "end-1" }
          ]
        }
        """;

        var defRes = await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Masked SmartObject Flow",
            graphJson,
            createdBy = "adel.sebti@corp.internal"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Start workflow instance
        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started!.Status.Should().Be("Completed");

        // 4. Standard user (non-Admin, no instance.view_sensitive) inspects ViewFlow & InstanceEvents -> masked with "***"
        var stdFlow = await standardClient.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        stdFlow.Should().NotBeNull();
        stdFlow!.Variables["employeeName"]?.ToString().Should().Be("Sami Benali");
        stdFlow.Variables["employeeSecretCode"]?.ToString().Should().Be("***");
        stdFlow.Variables["employeeComp"]?.ToString().Should().Be("***");

        var stdTimelineJson = JsonSerializer.Serialize(stdFlow.Timeline);
        stdTimelineJson.Should().NotContain("ULTRA-CONFIDENTIAL-7788");
        stdTimelineJson.Should().Contain("***");

        var stdEvents = await standardClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        stdEvents.Should().NotBeNull();
        var stdInvoked = stdEvents!.Events.First(e => e.Type == "SmartObject.Invoked");
        stdInvoked.PayloadJson.Should().NotContain("ULTRA-CONFIDENTIAL-7788");
        stdInvoked.PayloadJson.Should().Contain("\"confidential_code\":\"***\"");

        // 5. Admin user inspects ViewFlow & InstanceEvents -> sees real unmasked values
        var adminFlow = await adminClient.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        adminFlow!.Variables["employeeSecretCode"]?.ToString().Should().Be("ULTRA-CONFIDENTIAL-7788");

        var adminEvents = await adminClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        var adminInvoked = adminEvents!.Events.First(e => e.Type == "SmartObject.Invoked");
        adminInvoked.PayloadJson.Should().Contain("ULTRA-CONFIDENTIAL-7788");
    }
}
