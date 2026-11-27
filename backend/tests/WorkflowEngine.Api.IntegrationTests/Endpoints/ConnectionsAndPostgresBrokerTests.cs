using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Services.Brokers;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

#region Configurable In-Memory PostgreSQL Test Provider (for environments without Docker)

public sealed record ExecutedSqlCommand(string CommandText, Dictionary<string, object?> Parameters, int CommandTimeout);

public sealed class FakePostgresStore : IPostgresConnectionFactory
{
    public ConcurrentQueue<ExecutedSqlCommand> ExecutedCommands { get; } = new();
    public Dictionary<string, List<string>> TableColumns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Dictionary<string, object?>> Rows { get; } = new();
    public bool FailNextOpenWithSensitiveError { get; set; }
    public string SensitiveErrorToThrow { get; set; } = string.Empty;

    public FakePostgresStore()
    {
        TableColumns["public.customers"] = new List<string> { "id", "name", "email", "status" };
    }

    public DbConnection CreateConnection(string connectionString)
        => new FakePostgresDbConnection(connectionString, this);
}

internal sealed class FakePostgresDbConnection : DbConnection
{
    private readonly FakePostgresStore _store;
    private ConnectionState _state = ConnectionState.Closed;

    public FakePostgresDbConnection(string connectionString, FakePostgresStore store)
    {
        ConnectionString = connectionString;
        _store = store;
    }

    [AllowNull]
    public override string ConnectionString { get; set; }
    public override string Database => "fake_pg";
    public override string DataSource => "fake_host";
    public override string ServerVersion => "16.0";
    public override ConnectionState State => _state;

    public override void Open()
    {
        if (_store.FailNextOpenWithSensitiveError)
        {
            _store.FailNextOpenWithSensitiveError = false;
            throw new InvalidOperationException(_store.SensitiveErrorToThrow);
        }

        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    public override void Close() => _state = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) { }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => new FakePostgresDbCommand(this, _store);
}

internal sealed class FakePostgresDbCommand : DbCommand
{
    private readonly FakePostgresStore _store;
    private readonly FakeDbParameterCollection _parameters = new();

    public FakePostgresDbCommand(DbConnection connection, FakePostgresStore store)
    {
        DbConnection = connection;
        _store = store;
    }

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    private Dictionary<string, object?> SnapshotParams()
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DbParameter p in _parameters)
        {
            dict[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
        }
        return dict;
    }

    public override object? ExecuteScalar()
    {
        var p = SnapshotParams();
        _store.ExecutedCommands.Enqueue(new ExecutedSqlCommand(CommandText, p, CommandTimeout));
        if (CommandText.Trim().Equals("SELECT 1", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        return null;
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        => Task.FromResult(ExecuteScalar());

    public override int ExecuteNonQuery()
    {
        var p = SnapshotParams();
        _store.ExecutedCommands.Enqueue(new ExecutedSqlCommand(CommandText, p, CommandTimeout));

        if (CommandText.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase))
        {
            if (p.TryGetValue("@w0", out var idVal))
            {
                int removed = _store.Rows.RemoveAll(r => Equals(r.GetValueOrDefault("id")?.ToString(), idVal?.ToString()));
                return removed;
            }
        }
        return 0;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        => Task.FromResult(ExecuteNonQuery());

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var p = SnapshotParams();
        _store.ExecutedCommands.Enqueue(new ExecutedSqlCommand(CommandText, p, CommandTimeout));

        // 1. information_schema.columns query
        if (CommandText.Contains("information_schema.columns", StringComparison.OrdinalIgnoreCase))
        {
            string schema = p.GetValueOrDefault("@schema")?.ToString() ?? "public";
            string table = p.GetValueOrDefault("@table")?.ToString() ?? string.Empty;
            string key = $"{schema}.{table}";
            var cols = _store.TableColumns.TryGetValue(key, out var list) ? list : new List<string>();
            var metaRows = cols.Select(c => new Dictionary<string, object?> { ["column_name"] = c }).ToList();
            return new FakeDbDataReader(metaRows, new[] { "column_name" });
        }

        // 2. INSERT INTO ... RETURNING *
        if (CommandText.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
        {
            var newRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            int idx = 0;
            // Parse quoted column names between first '(' and ')'
            int openParen = CommandText.IndexOf('(');
            int closeParen = CommandText.IndexOf(')');
            if (openParen > 0 && closeParen > openParen)
            {
                var colNames = CommandText[(openParen + 1)..closeParen]
                    .Split(',')
                    .Select(c => c.Trim().Trim('"'))
                    .ToList();
                foreach (var col in colNames)
                {
                    newRow[col] = p.GetValueOrDefault($"@v{idx++}");
                }
            }
            _store.Rows.Add(newRow);
            return new FakeDbDataReader(new List<Dictionary<string, object?>> { newRow }, newRow.Keys.ToArray());
        }

        // 3. UPDATE ... SET ... WHERE ... RETURNING *
        if (CommandText.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase))
        {
            var targetId = p.GetValueOrDefault("@w0")?.ToString();
            var matched = _store.Rows.Where(r => Equals(r.GetValueOrDefault("id")?.ToString(), targetId)).ToList();
            int setIdx = CommandText.IndexOf(" SET ", StringComparison.OrdinalIgnoreCase);
            int whereIdx = CommandText.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
            if (setIdx > 0 && whereIdx > setIdx)
            {
                var assignments = CommandText[(setIdx + 5)..whereIdx].Split(',');
                int idx = 0;
                foreach (var a in assignments)
                {
                    string col = a.Split('=')[0].Trim().Trim('"');
                    object? val = p.GetValueOrDefault($"@s{idx++}");
                    foreach (var row in matched)
                    {
                        row[col] = val;
                    }
                }
            }
            var cols = matched.FirstOrDefault()?.Keys.ToArray() ?? new[] { "id", "name", "email", "status" };
            return new FakeDbDataReader(matched, cols);
        }

        // 4. SELECT * FROM ...
        var resultRows = _store.Rows.AsEnumerable();
        if (CommandText.Contains(" WHERE ", StringComparison.OrdinalIgnoreCase) && p.TryGetValue("@w0", out var filterVal))
        {
            // Match whichever column is in WHERE "col" = @w0
            int whereIdx = CommandText.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
            string afterWhere = CommandText[(whereIdx + 7)..];
            string filterCol = afterWhere.Split('=')[0].Trim().Trim('"');
            resultRows = resultRows.Where(r => Equals(r.GetValueOrDefault(filterCol)?.ToString(), filterVal?.ToString()));
        }

        var materialized = resultRows.ToList();
        var colList = materialized.FirstOrDefault()?.Keys.ToArray() ?? new[] { "id", "name", "email", "status" };
        return new FakeDbDataReader(materialized, colList);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        => Task.FromResult(ExecuteDbDataReader(behavior));
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = new();
    public override int Count => _items.Count;
    public override object SyncRoot => ((ICollection)_items).SyncRoot;
    public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) _items.Add((DbParameter)v); }
    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => _items.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) { int i = IndexOf(parameterName); if (i >= 0) _items.RemoveAt(i); }
    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}

internal sealed class FakeDbDataReader : DbDataReader
{
    private readonly List<Dictionary<string, object?>> _rows;
    private readonly string[] _columns;
    private int _index = -1;

    public FakeDbDataReader(List<Dictionary<string, object?>> rows, string[] columns)
    {
        _rows = rows;
        _columns = columns;
    }

    public override int FieldCount => _columns.Length;
    public override bool HasRows => _rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => _rows.Count;
    public override int Depth => 0;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool Read() => ++_index < _rows.Count;
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
    public override string GetName(int ordinal) => _columns[ordinal];
    public override int GetOrdinal(string name) => Array.FindIndex(_columns, c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
    public override object GetValue(int ordinal) => _rows[_index].GetValueOrDefault(_columns[ordinal]) ?? DBNull.Value;
    public override bool IsDBNull(int ordinal) => _rows[_index].GetValueOrDefault(_columns[ordinal]) == null;
    public override string GetString(int ordinal) => GetValue(ordinal)?.ToString() ?? string.Empty;
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override Type GetFieldType(int ordinal) => GetValue(ordinal)?.GetType() ?? typeof(object);
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => Guid.Parse(GetString(ordinal));
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override int GetValues(object[] values) { for (int i = 0; i < FieldCount; i++) values[i] = GetValue(i); return FieldCount; }
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => _rows.GetEnumerator();
}

#endregion

public class ConnectionsBrokerTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineConnBrokerTestDb_{Guid.NewGuid():N}";

    public const string LegacySecretValue = "legacy-rest-secret-token-777";
    public RecordingHttpTransport Transport { get; } = new();
    public StubDnsResolver DnsResolver { get; } = new();
    public FakePostgresStore PostgresStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");
        builder.UseSetting("Seed:PrimaryAdmin:Email", "adel.sebti@corp.internal");
        builder.UseSetting("Secrets:LEGACY_REST_TOKEN", LegacySecretValue);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpConnectorTransport>();
            services.AddSingleton<IHttpConnectorTransport>(Transport);

            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver>(DnsResolver);

            services.RemoveAll<IPostgresConnectionFactory>();
            services.AddSingleton<IPostgresConnectionFactory>(PostgresStore);
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
/// Étape E.2a — Connection model, ConnectionPermission governance, UserConnectionCredential personal vault,
/// USER_CREDENTIAL_REQUIRED wait-and-resume engine flow, /test sanitization, and Rest/Postgres Service Brokers.
/// </summary>
public class ConnectionsAndPostgresBrokerTests : IClassFixture<ConnectionsBrokerTestFactory>
{
    private readonly ConnectionsBrokerTestFactory _factory;

    public ConnectionsAndPostgresBrokerTests(ConnectionsBrokerTestFactory factory)
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
    public async Task ExistingRestConnections_RemainFullyFunctionalAfterMigration_WithDefaultRestAndStaticMode()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"legacy-rest-{suffix}";
        string pathPrefix = $"/v1/legacy-{suffix}";

        // Create a connection using ONLY legacy fields (omitting ServiceType, AuthMode, SecretSource)
        var createRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = connName,
            baseUrl = "https://api.partner.test",
            authType = "bearer",
            secretRef = "LEGACY_REST_TOKEN",
            allowedHosts = new[] { "api.partner.test" }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createRes.Content.ReadFromJsonAsync<ConnectionDto>();
        created.Should().NotBeNull();
        created!.ServiceType.Should().Be("rest");
        created.AuthMode.Should().Be("static");
        created.SecretSource.Should().Be("internal_encrypted");

        // Stub REST response and test connectivity + invocation via RestServiceBroker
        _factory.Transport.RegisterHandler("/", (_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"ok"}""", Encoding.UTF8, "application/json")
            });

        _factory.Transport.RegisterHandler(pathPrefix, (req, _) =>
        {
            req.Headers.Authorization?.ToString().Should().Be($"Bearer {ConnectionsBrokerTestFactory.LegacySecretValue}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"orderId":"ORD-99"}""", Encoding.UTF8, "application/json")
            };
        });

        var testRes = await adminClient.PostAsync($"/api/connections/{created.Id}/test", null);
        testRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var testBody = await testRes.Content.ReadFromJsonAsync<ConnectionTestResult>();
        testBody!.Success.Should().BeTrue();

        var invokeRes = await adminClient.PostAsJsonAsync($"/api/connections/{created.Id}/invoke", new
        {
            operation = "get",
            path = pathPrefix,
            httpMethod = "GET"
        });
        invokeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var invokeBody = await invokeRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        invokeBody!.Success.Should().BeTrue();
        invokeBody.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ConnectionPermissions_DesignerWithoutCanUse_Receives403WhenInvokingConnection()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var designerClient = await CreateAuthenticatedClientAsync("designer@tamam.local");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"restricted-conn-{suffix}";

        // Admin creates a connection without granting Designer CanUse
        var createRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = connName,
            baseUrl = "https://api.partner.test",
            authType = "none",
            allowedHosts = new[] { "api.partner.test" }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<ConnectionDto>();
        created.Should().NotBeNull();

        // Designer cannot see it in GET /api/connections
        var designerList = await designerClient.GetFromJsonAsync<List<ConnectionDto>>("/api/connections");
        designerList!.Should().NotContain(c => c.Id == created!.Id);

        // Designer receives 403 Forbidden when attempting to invoke or test it
        var invokeForbidden = await designerClient.PostAsJsonAsync($"/api/connections/{created!.Id}/invoke", new
        {
            operation = "get",
            path = "/v1/ping",
            httpMethod = "GET"
        });
        invokeForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var testForbidden = await designerClient.PostAsync($"/api/connections/{created.Id}/test", null);
        testForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Admin grants CanUse (without CanManage) to designer@tamam.local
        var grantRes = await adminClient.PutAsJsonAsync($"/api/connections/{created.Id}/permissions", new
        {
            subjectType = "User",
            subjectId = "designer@tamam.local",
            canUse = true,
            canManage = false
        });
        grantRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now Designer sees it and can test/invoke, but still gets 403 on DELETE (requires CanManage)
        var designerListAfter = await designerClient.GetFromJsonAsync<List<ConnectionDto>>("/api/connections");
        designerListAfter!.Should().Contain(c => c.Id == created.Id && c.CanUse && !c.CanManage);

        var deleteForbidden = await designerClient.DeleteAsync($"/api/connections/{created.Id}");
        deleteForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserSsoVault_WhenCredentialsMissing_TransitionsInstanceToWaitingAndCreatesCredentialTask()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"user-vault-conn-{suffix}";
        string pathPrefix = $"/v1/personal-{suffix}";
        string userEmail = $"vault-user-{suffix}@corp.internal";

        // Create a dedicated user
        var createUserRes = await adminClient.PostAsJsonAsync("/api/users", new
        {
            name = "Vault Test User",
            email = userEmail,
            role = "End User",
            group = "Operations",
            status = "Active",
            password = DatabaseInitializer.DefaultSeedPassword
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Admin creates a user_sso_vault connection
        var createConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = connName,
            serviceType = "rest",
            baseUrl = "https://api.partner.test",
            authType = "bearer",
            authMode = "user_sso_vault",
            secretSource = "internal_encrypted",
            allowedHosts = new[] { "api.partner.test" }
        });
        createConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        // Create a workflow with Trigger -> HTTP (using user_sso_vault connection) -> End
        string wfKey = $"wf-vault-{suffix}";
        string graphJson = $$$"""
        {
          "nodes": [
            { "id": "start-1", "data": { "nodeType": "trigger", "label": "Start" } },
            {
              "id": "http-1",
              "data": {
                "nodeType": "http",
                "label": "Call Personal API",
                "httpData": {
                  "connectionRef": "{{{connName}}}",
                  "method": "GET",
                  "path": "{{{pathPrefix}}}",
                  "responseMapping": { "resultCode": "$.code" }
                }
              }
            },
            { "id": "end-1", "data": { "nodeType": "end", "label": "Done" } }
          ],
          "edges": [
            { "id": "e1", "source": "start-1", "target": "http-1" },
            { "id": "e2", "source": "http-1", "target": "end-1" }
          ]
        }
        """;

        var defRes = await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = $"Vault Workflow {suffix}",
            description = "Tests USER_CREDENTIAL_REQUIRED pause and resume",
            category = "Operations",
            graphJson,
            createdBy = "adel.sebti@corp.internal"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Start workflow as userEmail who has NOT yet saved personal credentials
        var userClient = await CreateAuthenticatedClientAsync(userEmail);
        var startRes = await userClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = userEmail,
            triggerVariables = new Dictionary<string, object>()
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started.Should().NotBeNull();

        // Instance MUST be Waiting (NOT Faulted!)
        started!.Status.Should().Be("Waiting");

        // Verify a personal credential task was automatically created for userEmail
        var tasks = await userClient.GetFromJsonAsync<List<WorkflowTaskDto>>("/api/tasks");
        tasks.Should().NotBeNull();
        var credTask = tasks!.FirstOrDefault(t => t.InstanceId == started.InstanceId);
        credTask.Should().NotBeNull();
        credTask!.TaskTitle.Should().Be($"Renseignez vos identifiants pour {connName}");
        credTask.MetadataJson.Should().Contain(conn!.Id);
        credTask.MetadataJson.Should().Contain("requiredConnectionId");

        // User saves their personal credentials via self-service vault API
        const string personalToken = "my-personal-sso-bearer-token-54321";
        var saveCredRes = await userClient.PutAsJsonAsync($"/api/me/connection-credentials/{conn.Id}", new
        {
            secretJson = JsonSerializer.Serialize(new { token = personalToken })
        });
        saveCredRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify GET /api/me/connection-credentials never leaks the token
        var myCredsText = await userClient.GetStringAsync("/api/me/connection-credentials");
        myCredsText.Should().Contain(connName);
        myCredsText.Should().NotContain(personalToken);

        // Stub the external HTTP call and assert it receives the user's personal token
        _factory.Transport.RegisterHandler(pathPrefix, (req, _) =>
        {
            req.Headers.Authorization?.ToString().Should().Be($"Bearer {personalToken}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"code":"SYNC_OK"}""", Encoding.UTF8, "application/json")
            };
        });

        // User completes the credential task -> node re-executes and instance completes!
        var completeRes = await userClient.PostAsJsonAsync($"/api/tasks/{credTask.TaskId}/complete", new
        {
            outcome = "continue",
            comments = "Credentials saved"
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedResult = await completeRes.Content.ReadFromJsonAsync<TaskCompletedResult>();
        completedResult!.InstanceStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task TestConnectionEndpoint_SanitizesErrorMessages_NeverLeakingConnectionStringsOrPasswords()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        const string secretPassword = "SuperSecretPgPassword!2026";
        string rawConnStr = $"Host=pg-prod.internal;Port=5432;Database=erp;Username=pgadmin;Password={secretPassword}";

        var createRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"pg-sanitize-{suffix}",
            serviceType = "postgres",
            baseUrl = "postgres://pg-prod.internal:5432/erp",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            credentialsJson = JsonSerializer.Serialize(new { connectionString = rawConnStr })
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createRes.Content.ReadFromJsonAsync<ConnectionDto>();

        // Configure fake Postgres driver to throw an exception containing the full connection string & password
        _factory.PostgresStore.FailNextOpenWithSensitiveError = true;
        _factory.PostgresStore.SensitiveErrorToThrow =
            $"28P01: password authentication failed for user pgadmin on {rawConnStr} with password={secretPassword}";

        var testRes = await adminClient.PostAsync($"/api/connections/{conn!.Id}/test", null);
        testRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var bodyText = await testRes.Content.ReadAsStringAsync();
        bodyText.Should().NotContain(secretPassword);
        bodyText.Should().NotContain(rawConnStr);

        var result = JsonSerializer.Deserialize<ConnectionTestResult>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("[REDACTED");
    }

    [Fact]
    public async Task PostgresServiceBroker_ExecutesParameterizedCrud_AndPreventsSqlInjection()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];

        var createConnRes = await adminClient.PostAsJsonAsync("/api/connections", new
        {
            name = $"pg-crud-{suffix}",
            serviceType = "postgres",
            baseUrl = "postgres://pg-test.internal:5432/crm",
            authType = "basic",
            authMode = "static",
            secretSource = "internal_encrypted",
            credentialsJson = JsonSerializer.Serialize(new
            {
                host = "pg-test.internal",
                port = "5432",
                database = "crm",
                username = "crm_user",
                password = "pg-secret-password"
            })
        });
        createConnRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnRes.Content.ReadFromJsonAsync<ConnectionDto>();

        // 1. Test connection -> executes SELECT 1 with timeout = 5s
        var testRes = await adminClient.PostAsync($"/api/connections/{conn!.Id}/test", null);
        testRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var testBody = await testRes.Content.ReadFromJsonAsync<ConnectionTestResult>();
        testBody!.Success.Should().BeTrue();
        _factory.PostgresStore.ExecutedCommands.Should().Contain(c => c.CommandText == "SELECT 1" && c.CommandTimeout == 5);

        // 2. CREATE (parameterized INSERT with malicious-looking SQL payload in value -> treated strictly as parameter)
        const string sqliPayloadValue = "Alice'; DROP TABLE customers;--";
        var createRowRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Create",
            schemaName = "public",
            tableName = "customers",
            parameters = new Dictionary<string, object?>
            {
                ["values"] = new Dictionary<string, object?>
                {
                    ["id"] = "cust-1",
                    ["name"] = sqliPayloadValue,
                    ["email"] = "alice@example.com",
                    ["status"] = "Active"
                }
            }
        });
        createRowRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var createRowBody = await createRowRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        createRowBody!.Success.Should().BeTrue();
        createRowBody.AffectedRows.Should().Be(1);

        // Assert CommandText NEVER contains the raw payload (strictly parameterized) and CommandTimeout == 15
        var insertCmd = _factory.PostgresStore.ExecutedCommands.Last(c => c.CommandText.StartsWith("INSERT INTO"));
        insertCmd.CommandTimeout.Should().Be(15);
        insertCmd.CommandText.Should().NotContain(sqliPayloadValue);
        insertCmd.Parameters.Values.Should().Contain(sqliPayloadValue);

        // 3. GET
        var getRowRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Get",
            tableName = "customers",
            parameters = new Dictionary<string, object?>
            {
                ["where"] = new Dictionary<string, object?> { ["id"] = "cust-1" }
            }
        });
        var getRowBody = await getRowRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        getRowBody!.Success.Should().BeTrue();
        getRowBody.Rows.Should().HaveCount(1);

        // 4. UPDATE
        var updateRowRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Update",
            tableName = "customers",
            parameters = new Dictionary<string, object?>
            {
                ["values"] = new Dictionary<string, object?> { ["status"] = "VIP" },
                ["where"] = new Dictionary<string, object?> { ["id"] = "cust-1" }
            }
        });
        var updateRowBody = await updateRowRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        updateRowBody!.Success.Should().BeTrue();
        updateRowBody.AffectedRows.Should().Be(1);

        // 5. SQL Injection via Table Name -> blocked by information_schema.columns validation
        var sqliTableRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            tableName = "customers; DROP TABLE customers;--"
        });
        var sqliTableBody = await sqliTableRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        sqliTableBody!.Success.Should().BeFalse();
        sqliTableBody.ErrorCode.Should().Be("POSTGRES_INVALID_TABLE");

        // 6. SQL Injection via Column Name -> blocked by information_schema.columns validation
        var sqliColRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "List",
            tableName = "customers",
            parameters = new Dictionary<string, object?>
            {
                ["where"] = new Dictionary<string, object?> { ["id = '1' OR '1'='1"] = "x" }
            }
        });
        var sqliColBody = await sqliColRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        sqliColBody!.Success.Should().BeFalse();
        sqliColBody.ErrorCode.Should().Be("POSTGRES_INVALID_IDENTIFIER");

        // 7. DELETE
        var deleteRowRes = await adminClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new
        {
            operation = "Delete",
            tableName = "customers",
            parameters = new Dictionary<string, object?>
            {
                ["where"] = new Dictionary<string, object?> { ["id"] = "cust-1" }
            }
        });
        var deleteRowBody = await deleteRowRes.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        deleteRowBody!.Success.Should().BeTrue();
        deleteRowBody.AffectedRows.Should().Be(1);
    }
}
