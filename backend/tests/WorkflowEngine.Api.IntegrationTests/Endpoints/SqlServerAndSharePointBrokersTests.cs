using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Services.Brokers;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

#region Test Doubles for SQL Server & SharePoint Brokers

public sealed record RecordedSqlServerCommand(
    string CommandText,
    Dictionary<string, object?> Parameters,
    int CommandTimeout);

public sealed class FakeSqlServerCommandRunner : ISqlServerCommandRunner
{
    public ConcurrentQueue<RecordedSqlServerCommand> ExecutedCommands { get; } = new();
    public Dictionary<string, List<string>> TableColumns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Dictionary<string, object?>> Rows { get; } = new();

    public FakeSqlServerCommandRunner()
    {
        TableColumns["dbo.employees"] = new List<string> { "id", "full_name", "department", "status" };
        Rows.Add(new Dictionary<string, object?> { ["id"] = 1, ["full_name"] = "Alice Martin", ["department"] = "Finance", ["status"] = "Active" });
        Rows.Add(new Dictionary<string, object?> { ["id"] = 2, ["full_name"] = "Bob Dupont", ["department"] = "IT", ["status"] = "Active" });
    }

    private static Dictionary<string, object?> Snapshot(IReadOnlyList<SqlParameter> parameters)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            dict[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
        }
        return dict;
    }

    public Task<object?> ExecuteScalarAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        ExecutedCommands.Enqueue(new RecordedSqlServerCommand(commandText, Snapshot(parameters), commandTimeoutSeconds));
        if (commandText.Trim().Equals("SELECT 1", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<object?>(1);
        }

        return Task.FromResult<object?>(null);
    }

    public Task<SqlServerQueryResult> ExecuteQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        int maxResponseBytes,
        CancellationToken ct = default)
    {
        var snap = Snapshot(parameters);
        ExecutedCommands.Enqueue(new RecordedSqlServerCommand(commandText, snap, commandTimeoutSeconds));

        // 1. Metadata query against sys.columns / sys.tables
        if (commandText.Contains("sys.columns", StringComparison.OrdinalIgnoreCase))
        {
            string schema = snap.GetValueOrDefault("@schema")?.ToString() ?? "dbo";
            string table = snap.GetValueOrDefault("@table")?.ToString() ?? string.Empty;
            string key = $"{schema}.{table}";
            var cols = TableColumns.TryGetValue(key, out var list) ? list : new List<string>();
            var metaRows = cols.Select(c => new Dictionary<string, object?> { ["column_name"] = c }).ToList();
            return Task.FromResult(new SqlServerQueryResult(metaRows, metaRows.Count));
        }

        // 2. INSERT INTO ... OUTPUT INSERTED.*
        if (commandText.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
        {
            var newRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in snap)
            {
                newRow[kv.Key] = kv.Value;
            }
            Rows.Add(newRow);
            return Task.FromResult(new SqlServerQueryResult(new List<Dictionary<string, object?>> { newRow }, 1));
        }

        // 3. SELECT queries
        return Task.FromResult(new SqlServerQueryResult(Rows.ToList(), Rows.Count));
    }

    public Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        ExecutedCommands.Enqueue(new RecordedSqlServerCommand(commandText, Snapshot(parameters), commandTimeoutSeconds));
        return Task.FromResult(1);
    }
}

public sealed class ConfigurableDnsResolver : IDnsResolver
{
    private readonly ConcurrentDictionary<string, IPAddress[]> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public ConfigurableDnsResolver()
    {
        _overrides["graph.microsoft.com"] = new[] { IPAddress.Parse("20.190.151.68") };
        _overrides["login.microsoftonline.com"] = new[] { IPAddress.Parse("20.190.151.69") };
        _overrides["private.sharepoint.test"] = new[] { IPAddress.Parse("169.254.169.254") };
    }

    public void SetHost(string host, params IPAddress[] addresses)
    {
        _overrides[host] = addresses;
    }

    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(host, out var ips))
        {
            return Task.FromResult(ips);
        }

        return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
    }
}

public sealed class StubSharePointHttpMessageHandler : HttpMessageHandler
{
    public ConcurrentQueue<HttpRequestMessage> SentRequests { get; } = new();
    public Func<HttpRequestMessage, int, HttpResponseMessage>? Handler { get; set; }
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _callCount, 0);
        while (SentRequests.TryDequeue(out _)) { }
        Handler = null;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int attempt = Interlocked.Increment(ref _callCount);
        SentRequests.Enqueue(request);

        if (Handler != null)
        {
            return Task.FromResult(Handler(request, attempt));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value":[]}""", Encoding.UTF8, "application/json")
        });
    }
}

#endregion

public class SqlServerAndSharePointTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineSqlSpTestDb_{Guid.NewGuid():N}";

    public FakeSqlServerCommandRunner SqlServerRunner { get; } = new();
    public ConfigurableDnsResolver DnsResolver { get; } = new();
    public StubSharePointHttpMessageHandler GraphHandler { get; } = new();
    public StubSharePointHttpMessageHandler TokenHandler { get; } = new();

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

            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver>(DnsResolver);

            // Preserve the Polly resilience pipeline registered on SharePointGraph while swapping only the primary handler
            services.AddHttpClient(SharePointServiceBroker.GraphHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => GraphHandler);

            services.AddHttpClient(SharePointServiceBroker.TokenHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => TokenHandler);
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

/// <summary>
/// Étape E.2b — SQL Server &amp; SharePoint Graph Service Brokers with Write Guard (AllowWrite = false),
/// MaxRows hard cap (1000), SQL identifier whitelisting, Polly 429 Retry-After resilience, SSRF guard,
/// and reversible EF Core migration AddConnectionWriteGuardAndRowLimits.
/// </summary>
public class SqlServerAndSharePointBrokersTests : IClassFixture<SqlServerAndSharePointTestFactory>
{
    private readonly SqlServerAndSharePointTestFactory _factory;

    public SqlServerAndSharePointBrokersTests(SqlServerAndSharePointTestFactory factory)
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

    [Fact]
    public async Task SqlServerBroker_WhenAllowWriteIsFalse_RejectsCreateUpdateDeleteWith403()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        // Create SQL Server connection with default settings (AllowWrite must default to false, MaxRows = 200)
        var createConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sql-ro-{suffix}",
            serviceType = "sqlserver",
            baseUrl = "sqlserver://sql-prod.internal:1433/ERP",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            credentialsJson = JsonSerializer.Serialize(new
            {
                server = "sql-prod.internal",
                database = "ERP",
                username = "erp_readonly",
                password = "ro-secret-password"
            })
        });
        createConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnRes.Content.ReadFromJsonAsync<ConnectionDto>();
        conn.Should().NotBeNull();
        conn!.AllowWrite.Should().BeFalse();
        conn.MaxRows.Should().Be(200);

        int commandsBefore = _factory.SqlServerRunner.ExecutedCommands.Count;

        // Create, Update, and Delete MUST all be blocked with 403 WRITE_NOT_ALLOWED_ON_CONNECTION
        foreach (string op in new[] { "Create", "Update", "Delete" })
        {
            var writeRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
            {
                operation = op,
                schemaName = "dbo",
                tableName = "employees",
                parameters = new Dictionary<string, object?>
                {
                    ["values"] = new Dictionary<string, object?> { ["full_name"] = "Blocked Write" },
                    ["where"] = new Dictionary<string, object?> { ["id"] = 1 }
                }
            });

            writeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"operation '{op}' must be blocked when AllowWrite == false");
            string errBody = await writeRes.Content.ReadAsStringAsync();
            errBody.Should().Contain(ConnectionWriteNotAllowedException.ErrorCode);
        }

        // No SQL commands should have been sent to SQL Server for blocked writes
        _factory.SqlServerRunner.ExecutedCommands.Count.Should().Be(commandsBefore);

        // Enabling AllowWrite via PUT allows Create to succeed with CommandTimeout = 15
        var updateConnRes = await adminClient.PutAsJsonAsync($"/api/connections/{conn.Id}", new
        {
            name = conn.Name,
            serviceType = "sqlserver",
            baseUrl = conn.BaseUrl,
            authType = conn.AuthType,
            authMode = conn.AuthMode,
            secretSource = conn.SecretSource,
            allowWrite = true,
            maxRows = 200,
            expectedVersion = conn.Version
        });
        updateConnRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedConn = await updateConnRes.Content.ReadFromJsonAsync<ConnectionDto>();
        updatedConn!.AllowWrite.Should().BeTrue();

        var allowedCreateRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Create",
            schemaName = "dbo",
            tableName = "employees",
            parameters = new Dictionary<string, object?>
            {
                ["values"] = new Dictionary<string, object?>
                {
                    ["full_name"] = "Charlie Allowed",
                    ["department"] = "Ops",
                    ["status"] = "Active"
                }
            }
        });
        allowedCreateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var allowedBody = await allowedCreateRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        allowedBody!.Success.Should().BeTrue();

        var insertCmd = _factory.SqlServerRunner.ExecutedCommands.Last(c => c.CommandText.StartsWith("INSERT INTO"));
        insertCmd.CommandTimeout.Should().Be(15);
        insertCmd.CommandText.Should().NotContain("Charlie Allowed");
        insertCmd.Parameters.Values.Should().Contain("Charlie Allowed");
    }

    [Fact]
    public async Task SqlServerBroker_EnforcesMaxRowsCap1000_AndRejectsUnwhitelistedIdentifiers()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        // 1. Creating a connection with MaxRows > 1000 must be rejected with 400 Bad Request
        var invalidMaxRowsRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sql-overcap-{suffix}",
            serviceType = "sqlserver",
            baseUrl = "sqlserver://sql-prod.internal:1433/ERP",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            maxRows = 1500
        });
        invalidMaxRowsRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Create connection with maxRows = 1000
        var createConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sql-cap-{suffix}",
            serviceType = "sqlserver",
            baseUrl = "sqlserver://sql-prod.internal:1433/ERP",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            maxRows = 1000,
            credentialsJson = JsonSerializer.Serialize(new
            {
                server = "sql-prod.internal",
                database = "ERP",
                username = "erp_ro",
                password = "secret"
            })
        });
        createConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        // 3. List with pageSize = 5000 must be clamped to @maxRows = 1000 using TOP (@maxRows)
        var listRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn!.Id}/invoke", new
        {
            operation = "List",
            schemaName = "dbo",
            tableName = "employees",
            parameters = new Dictionary<string, object?>
            {
                ["page"] = 1,
                ["pageSize"] = 5000
            }
        });
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        listBody!.Success.Should().BeTrue();

        var topCmd = _factory.SqlServerRunner.ExecutedCommands.Last(c => c.CommandText.StartsWith("SELECT TOP (@maxRows)"));
        topCmd.CommandTimeout.Should().Be(15);
        topCmd.Parameters["@maxRows"].Should().Be(1000);

        // 4. List with page = 2, pageSize = 50 uses OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
        var pagedRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            schemaName = "dbo",
            tableName = "employees",
            parameters = new Dictionary<string, object?>
            {
                ["page"] = 2,
                ["pageSize"] = 50
            }
        });
        pagedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedCmd = _factory.SqlServerRunner.ExecutedCommands.Last(c => c.CommandText.Contains("OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY"));
        pagedCmd.Parameters["@offset"].Should().Be(50);
        pagedCmd.Parameters["@pageSize"].Should().Be(50);

        // 5. Forbidden schema (sys / INFORMATION_SCHEMA) is blocked immediately
        var sysSchemaRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            schemaName = "sys",
            tableName = "tables"
        });
        var sysSchemaBody = await sysSchemaRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        sysSchemaBody!.Success.Should().BeFalse();
        sysSchemaBody.ErrorCode.Should().Be("SQLSERVER_FORBIDDEN_SCHEMA");

        // 6. Unwhitelisted table / SQL injection attempt is rejected
        var badTableRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            schemaName = "dbo",
            tableName = "employees; DROP TABLE employees;--"
        });
        var badTableBody = await badTableRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        badTableBody!.Success.Should().BeFalse();
        badTableBody.ErrorCode.Should().Be("SQLSERVER_INVALID_TABLE");

        // 7. Unwhitelisted column name is rejected
        var badColRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            schemaName = "dbo",
            tableName = "employees",
            parameters = new Dictionary<string, object?>
            {
                ["where"] = new Dictionary<string, object?>
                {
                    ["id; DROP TABLE employees;--"] = 1
                }
            }
        });
        var badColBody = await badColRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        badColBody!.Success.Should().BeFalse();
        badColBody.ErrorCode.Should().Be("SQLSERVER_INVALID_IDENTIFIER");
    }

    [Fact]
    public async Task SharePointBroker_Handles429RetryAfterWithPolly_AndFetchesListItems()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        _factory.GraphHandler.Reset();
        _factory.TokenHandler.Reset();

        // Stub Entra ID OAuth2 client_credentials token endpoint
        _factory.TokenHandler.Handler = (req, _) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"entra-sp-token-999","expires_in":3600}""", Encoding.UTF8, "application/json")
            };
        };

        // Stub Microsoft Graph /sites/.../lists/invoices/items:
        // Attempt 1 -> 429 Too Many Requests with Retry-After: 1
        // Attempt 2 -> 200 OK with SharePoint list items
        _factory.GraphHandler.Handler = (req, attempt) =>
        {
            req.Headers.Authorization?.ToString().Should().Be("Bearer entra-sp-token-999");

            if (attempt == 1)
            {
                var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return throttled;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "value": [
                    { "id": "101", "fields": { "Title": "INV-2026-001", "Amount": 1250 } },
                    { "id": "102", "fields": { "Title": "INV-2026-002", "Amount": 3400 } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        };

        var createConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sp-graph-{suffix}",
            serviceType = "sharepoint",
            baseUrl = "https://graph.microsoft.com/v1.0/sites/contoso-site-01",
            authType = "oauth2_client_credentials",
            authMode = "oauth2_client_credentials",
            secretSource = "internal_encrypted",
            allowedHosts = new[] { "graph.microsoft.com" },
            allowWrite = false,
            maxRows = 50,
            credentialsJson = JsonSerializer.Serialize(new
            {
                tenantId = "contoso-tenant-guid",
                clientId = "app-client-id",
                clientSecret = "super-secret-client-value"
            })
        });
        createConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        var sw = Stopwatch.StartNew();
        var invokeRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn!.Id}/invoke", new
        {
            operation = "List",
            tableName = "invoices",
            parameters = new Dictionary<string, object?>
            {
                ["pageSize"] = 500 // Must be clamped to connection.MaxRows (50)
            }
        });
        sw.Stop();

        invokeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await invokeRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.StatusCode.Should().Be(200);
        body.Rows.Should().HaveCount(2);

        // Polly must have retried after HTTP 429 and respected Retry-After: 1s
        _factory.GraphHandler.CallCount.Should().Be(2);
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(800);

        // Verify $top=50 was enforced on the Graph request URL
        var lastGraphReq = _factory.GraphHandler.SentRequests.Last();
        lastGraphReq.RequestUri!.Query.Should().Contain("$top=50");

        // Verify write operation on read-only SharePoint connection returns 403 WRITE_NOT_ALLOWED_ON_CONNECTION
        var writeForbiddenRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Create",
            tableName = "invoices",
            bodyJson = """{"fields":{"Title":"Blocked"}}"""
        });
        writeForbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string writeErr = await writeForbiddenRes.Content.ReadAsStringAsync();
        writeErr.Should().Contain(ConnectionWriteNotAllowedException.ErrorCode);
    }

    [Fact]
    public async Task SharePointBroker_BlocksPrivateIpOrRedirectsViaSsrfGuard()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        _factory.GraphHandler.Reset();

        // 1. Private / Link-Local IP resolved via DNS (169.254.169.254) MUST be blocked by ISsrfGuard
        var privateConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sp-ssrf-ip-{suffix}",
            serviceType = "sharepoint",
            baseUrl = "https://private.sharepoint.test/v1.0/sites/internal",
            authType = "bearer",
            authMode = "static",
            secretSource = "internal_encrypted",
            allowedHosts = new[] { "private.sharepoint.test" },
            credentialsJson = JsonSerializer.Serialize(new { token = "static-sp-token" })
        });
        privateConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var privateConn = await privateConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        var blockedIpRes = await adminClient.PostAsJsonAsync($"/api/connections/{privateConn!.Id}/invoke", new
        {
            operation = "List",
            tableName = "secrets"
        });
        blockedIpRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var blockedIpBody = await blockedIpRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        blockedIpBody!.Success.Should().BeFalse();
        blockedIpBody.ErrorCode.Should().Be("SSRF_BLOCKED");
        _factory.GraphHandler.CallCount.Should().Be(0);

        // 2. HTTP 302 Redirect from Graph endpoint MUST NOT be followed (AllowAutoRedirect = false)
        _factory.GraphHandler.Handler = (_, _) =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data/");
            return redirect;
        };

        var redirectConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"sp-ssrf-redir-{suffix}",
            serviceType = "sharepoint",
            baseUrl = "https://graph.microsoft.com/v1.0/sites/redirect-site",
            authType = "bearer",
            authMode = "static",
            secretSource = "internal_encrypted",
            allowedHosts = new[] { "graph.microsoft.com" },
            credentialsJson = JsonSerializer.Serialize(new { token = "static-sp-token" })
        });
        redirectConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var redirectConn = await redirectConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        var blockedRedirRes = await adminClient.PostAsJsonAsync($"/api/connections/{redirectConn!.Id}/invoke", new
        {
            operation = "List",
            tableName = "documents"
        });
        blockedRedirRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var blockedRedirBody = await blockedRedirRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        blockedRedirBody!.Success.Should().BeFalse();
        blockedRedirBody.ErrorCode.Should().Be("SSRF_REDIRECT_BLOCKED");
        blockedRedirBody.StatusCode.Should().Be(302);
        _factory.GraphHandler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Migration_AddConnectionWriteGuardAndRowLimits_IsReversibleUpAndDown()
    {
        string migrationTestDb = $"WorkflowEngineMigTestDb_{Guid.NewGuid():N}";
        string connStr = $"Server=(localdb)\\mssqllocaldb;Database={migrationTestDb};Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<WorkflowEngineDbContext>()
            .UseSqlServer(connStr, sql => sql.MigrationsAssembly(typeof(WorkflowEngineDbContext).Assembly.FullName))
            .Options;

        await using var db = new WorkflowEngineDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            var allMigrations = db.Database.GetMigrations().ToList();

            string targetMigration = allMigrations.Single(m => m.EndsWith("_AddConnectionWriteGuardAndRowLimits", StringComparison.Ordinal));
            int targetIdx = allMigrations.IndexOf(targetMigration);
            targetIdx.Should().BeGreaterThan(0);
            string previousMigration = allMigrations[targetIdx - 1];

            // 1. Migrate UP to AddConnectionWriteGuardAndRowLimits
            await migrator.MigrateAsync(targetMigration);
            var colsAfterUp = await GetConnectionColumnNamesAsync(connStr);
            colsAfterUp.Should().Contain("AllowWrite");
            colsAfterUp.Should().Contain("MaxRows");

            // 2. Migrate DOWN to previous migration
            await migrator.MigrateAsync(previousMigration);
            var colsAfterDown = await GetConnectionColumnNamesAsync(connStr);
            colsAfterDown.Should().NotContain("AllowWrite");
            colsAfterDown.Should().NotContain("MaxRows");

            // 3. Re-apply UP to verify full idempotent reversibility
            await migrator.MigrateAsync(targetMigration);
            var colsAfterReUp = await GetConnectionColumnNamesAsync(connStr);
            colsAfterReUp.Should().Contain("AllowWrite");
            colsAfterReUp.Should().Contain("MaxRows");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<List<string>> GetConnectionColumnNamesAsync(string connectionString)
    {
        var cols = new List<string>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Connections'";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cols.Add(reader.GetString(0));
        }

        return cols;
    }
}
