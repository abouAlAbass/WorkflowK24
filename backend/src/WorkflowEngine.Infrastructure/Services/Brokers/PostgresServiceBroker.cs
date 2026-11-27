using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.SmartObjects;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Services.Brokers;

/// <summary>
/// Configurable connection factory for PostgresServiceBroker (defaults to NpgsqlConnection in production;
/// replaceable in tests when Docker/Testcontainers is unavailable).
/// </summary>
public interface IPostgresConnectionFactory
{
    DbConnection CreateConnection(string connectionString);
}

public sealed class NpgsqlConnectionFactory : IPostgresConnectionFactory
{
    public DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);
}

/// <summary>
/// PostgreSQL Service Broker powered by Npgsql.
/// - TestConnectionAsync: runs 'SELECT 1' with strict 5s timeout and sanitized error output.
/// - InvokeAsync: executes Get, List, Create, Update, Delete using strictly parameterized queries
///   (CommandTimeout = 15s) after validating table and column identifiers against information_schema.columns.
/// </summary>
public class PostgresServiceBroker : IServiceBroker
{
    public const int TestTimeoutSeconds = 5;
    public const int CommandTimeoutSeconds = 15;

    private readonly IPostgresConnectionFactory _connectionFactory;

    public PostgresServiceBroker(IPostgresConnectionFactory? connectionFactory = null)
    {
        _connectionFactory = connectionFactory ?? new NpgsqlConnectionFactory();
    }

    public string ServiceType => "postgres";

    public async Task<ConnectionTestResult> TestConnectionAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var secretsToRedact = secret?.SecretsToRedact ?? Array.Empty<string>();

        if (!TryBuildConnectionString(connection, secret, TestTimeoutSeconds, out var connString, out var buildError))
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, ErrorSanitizer.Sanitize(buildError, secretsToRedact));
        }

        var allSecrets = new List<string>(secretsToRedact) { connString! };

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(TestTimeoutSeconds));

            await using var dbConn = _connectionFactory.CreateConnection(connString!);
            await dbConn.OpenAsync(timeoutCts.Token);

            await using var cmd = dbConn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = TestTimeoutSeconds;

            var scalar = await cmd.ExecuteScalarAsync(timeoutCts.Token);
            sw.Stop();

            bool ok = scalar != null && Convert.ToInt32(scalar) == 1;
            return ok
                ? new ConnectionTestResult(true, sw.ElapsedMilliseconds)
                : new ConnectionTestResult(false, sw.ElapsedMilliseconds, "Unexpected response from PostgreSQL health check.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, $"PostgreSQL connection test timed out after {TestTimeoutSeconds}s.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, ErrorSanitizer.Sanitize(ex.Message, allSecrets));
        }
    }

    public async Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        var secretsToRedact = secret?.SecretsToRedact ?? Array.Empty<string>();
        if (!TryBuildConnectionString(connection, secret, CommandTimeoutSeconds, out var connString, out var buildError))
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(buildError, secretsToRedact));
        }

        var allSecrets = new List<string>(secretsToRedact) { connString! };

        try
        {
            await using var dbConn = _connectionFactory.CreateConnection(connString!);
            await dbConn.OpenAsync(ct);

            // 1. Load primary keys per (table_schema, table_name)
            var pkByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            const string pkSql = """
                SELECT tc.table_schema, tc.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
                  AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY tc.table_schema, tc.table_name, kcu.ordinal_position
                """;

            await using (var pkCmd = dbConn.CreateCommand())
            {
                pkCmd.CommandText = pkSql;
                pkCmd.CommandTimeout = CommandTimeoutSeconds;
                await using var pkReader = await pkCmd.ExecuteReaderAsync(ct);
                while (await pkReader.ReadAsync(ct))
                {
                    string schema = pkReader.IsDBNull(0) ? "public" : pkReader.GetValue(0)?.ToString() ?? "public";
                    string table = pkReader.IsDBNull(1) ? string.Empty : pkReader.GetValue(1)?.ToString() ?? string.Empty;
                    string col = pkReader.IsDBNull(2) ? string.Empty : pkReader.GetValue(2)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(col)) continue;

                    string key = $"{schema}.{table}";
                    if (!pkByTable.TryGetValue(key, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        pkByTable[key] = set;
                    }
                    set.Add(col);
                }
            }

            // 2. Load tables/views and their columns from information_schema.columns
            const string colsSql = """
                SELECT c.table_schema, c.table_name, c.column_name, c.data_type, c.is_nullable
                FROM information_schema.columns c
                INNER JOIN information_schema.tables t
                    ON c.table_schema = t.table_schema AND c.table_name = t.table_name
                WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """;

            var grouped = new Dictionary<string, (string Schema, string Table, List<DiscoveredFieldDto> Fields)>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            await using (var colsCmd = dbConn.CreateCommand())
            {
                colsCmd.CommandText = colsSql;
                colsCmd.CommandTimeout = CommandTimeoutSeconds;
                await using var reader = await colsCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string schema = reader.IsDBNull(0) ? "public" : reader.GetValue(0)?.ToString() ?? "public";
                    string table = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1)?.ToString() ?? string.Empty;
                    string colName = reader.IsDBNull(2) ? string.Empty : reader.GetValue(2)?.ToString() ?? string.Empty;
                    string dataType = reader.IsDBNull(3) ? "text" : reader.GetValue(3)?.ToString() ?? "text";
                    string nullableRaw = reader.IsDBNull(4) ? "YES" : reader.GetValue(4)?.ToString() ?? "YES";
                    if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(colName)) continue;

                    string key = $"{schema}.{table}";
                    if (!grouped.TryGetValue(key, out var entry))
                    {
                        entry = (schema, table, new List<DiscoveredFieldDto>());
                        grouped[key] = entry;
                        order.Add(key);
                    }

                    bool isPk = pkByTable.TryGetValue(key, out var pkCols) && pkCols.Contains(colName);
                    bool isNullable = string.Equals(nullableRaw, "YES", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(nullableRaw, "true", StringComparison.OrdinalIgnoreCase);

                    entry.Fields.Add(new DiscoveredFieldDto(
                        Name: colName,
                        SourceType: dataType,
                        MappedSmartObjectType: SmartObjectTypeMapper.MapToSmartObjectType(dataType),
                        IsNullable: isNullable,
                        IsPrimaryKey: isPk,
                        SuggestedMasked: SmartObjectTypeMapper.IsSuggestedMasked(colName)));
                }
            }

            var entities = new List<DiscoveredEntityDto>(order.Count);
            foreach (var key in order)
            {
                var (schema, table, fields) = grouped[key];
                string entityName = string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase)
                    ? table
                    : $"{schema}.{table}";
                string? detectedPk = fields.FirstOrDefault(f => f.IsPrimaryKey)?.Name;

                entities.Add(new DiscoveredEntityDto(
                    EntityName: entityName,
                    DisplayName: SmartObjectTypeMapper.FormatDisplayName(table),
                    DetectedPrimaryKey: detectedPk,
                    Fields: fields,
                    SchemaName: schema,
                    EntityType: "Table"));
            }

            return entities;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(ex.Message, allSecrets), ex);
        }
    }

    public async Task<ServiceBrokerInvocationResult> InvokeAsync(
        ServiceBrokerInvocationRequest request,
        CancellationToken ct = default)
    {
        var secretsToRedact = request.Secret?.SecretsToRedact ?? Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_TABLE_REQUIRED", ErrorMessage: "TableName is required for PostgreSQL broker operations.");
        }

        string schemaName = string.IsNullOrWhiteSpace(request.SchemaName) ? "public" : request.SchemaName.Trim();
        string tableName = request.TableName.Trim();
        string operation = request.Operation?.Trim().ToLowerInvariant() ?? "list";

        if (!TryBuildConnectionString(request.Connection, request.Secret, CommandTimeoutSeconds, out var connString, out var buildError))
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_CONFIG_INVALID", ErrorMessage: ErrorSanitizer.Sanitize(buildError, secretsToRedact));
        }

        var allSecrets = new List<string>(secretsToRedact) { connString! };
        int cmdTimeout = Math.Clamp(request.TimeoutSeconds ?? CommandTimeoutSeconds, 1, 60);

        try
        {
            await using var dbConn = _connectionFactory.CreateConnection(connString!);
            await dbConn.OpenAsync(ct);

            // 1. Validate schema & table and load exact allowed column set from information_schema.columns
            var validColumns = await LoadTableColumnsAsync(dbConn, schemaName, tableName, cmdTimeout, ct);
            if (validColumns.Count == 0)
            {
                return new ServiceBrokerInvocationResult(
                    false,
                    ErrorCode: "POSTGRES_INVALID_TABLE",
                    ErrorMessage: $"Table '{schemaName}.{tableName}' does not exist or is not accessible.");
            }

            string quotedTable = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
            var parameters = request.Parameters ?? new Dictionary<string, object?>();

            return operation switch
            {
                "get" => await ExecuteGetAsync(dbConn, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "list" => await ExecuteListAsync(dbConn, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "create" => await ExecuteCreateAsync(dbConn, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "update" => await ExecuteUpdateAsync(dbConn, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "delete" => await ExecuteDeleteAsync(dbConn, quotedTable, validColumns, parameters, cmdTimeout, ct),
                _ => new ServiceBrokerInvocationResult(
                    false,
                    ErrorCode: "POSTGRES_UNSUPPORTED_OPERATION",
                    ErrorMessage: $"Unsupported PostgreSQL operation '{request.Operation}'. Allowed: Get, List, Create, Update, Delete.")
            };
        }
        catch (ArgumentException ex)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_INVALID_IDENTIFIER", ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "POSTGRES_EXECUTION_ERROR",
                ErrorMessage: ErrorSanitizer.Sanitize(ex.Message, allSecrets));
        }
    }

    private static async Task<HashSet<string>> LoadTableColumnsAsync(
        DbConnection conn,
        string schemaName,
        string tableName,
        int commandTimeout,
        CancellationToken ct)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = commandTimeout;
        AddParameter(cmd, "@schema", schemaName);
        AddParameter(cmd, "@table", tableName);

        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<ServiceBrokerInvocationResult> ExecuteGetAsync(
        DbConnection conn,
        string quotedTable,
        HashSet<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var filters = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);
        if (filters.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_FILTER_REQUIRED", ErrorMessage: "Get operation requires at least one filter column/value.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        var whereClauses = BuildParameterizedWhereClauses(filters, validColumns, cmd, "w");
        cmd.CommandText = $"SELECT * FROM {quotedTable} WHERE {string.Join(" AND ", whereClauses)} LIMIT 1";

        var rows = await ReadRowsAsync(cmd, ct);
        return new ServiceBrokerInvocationResult(true, Data: rows.FirstOrDefault(), Rows: rows, AffectedRows: rows.Count);
    }

    private static async Task<ServiceBrokerInvocationResult> ExecuteListAsync(
        DbConnection conn,
        string quotedTable,
        HashSet<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var filters = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);
        int limit = ExtractInt(parameters, "limit", 100, 1, 1000);
        int offset = ExtractInt(parameters, "offset", 0, 0, 100_000);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        string whereSql = string.Empty;
        if (filters.Count > 0)
        {
            var whereClauses = BuildParameterizedWhereClauses(filters, validColumns, cmd, "w");
            whereSql = $" WHERE {string.Join(" AND ", whereClauses)}";
        }

        AddParameter(cmd, "@limit", limit);
        AddParameter(cmd, "@offset", offset);
        cmd.CommandText = $"SELECT * FROM {quotedTable}{whereSql} LIMIT @limit OFFSET @offset";

        var rows = await ReadRowsAsync(cmd, ct);
        return new ServiceBrokerInvocationResult(true, Data: rows, Rows: rows, AffectedRows: rows.Count);
    }

    private static async Task<ServiceBrokerInvocationResult> ExecuteCreateAsync(
        DbConnection conn,
        string quotedTable,
        HashSet<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var values = ExtractDictionary(parameters, "values") ?? ExtractNonReservedParameters(parameters);
        if (values.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_VALUES_REQUIRED", ErrorMessage: "Create operation requires at least one column value.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        var cols = new List<string>();
        var paramNames = new List<string>();
        int idx = 0;

        foreach (var kv in values)
        {
            string exactCol = ResolveValidatedColumn(kv.Key, validColumns);
            string pName = $"@v{idx++}";
            cols.Add(QuoteIdentifier(exactCol));
            paramNames.Add(pName);
            AddParameter(cmd, pName, kv.Value);
        }

        cmd.CommandText = $"INSERT INTO {quotedTable} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", paramNames)}) RETURNING *";
        var rows = await ReadRowsAsync(cmd, ct);
        return new ServiceBrokerInvocationResult(true, Data: rows.FirstOrDefault(), Rows: rows, AffectedRows: rows.Count);
    }

    private static async Task<ServiceBrokerInvocationResult> ExecuteUpdateAsync(
        DbConnection conn,
        string quotedTable,
        HashSet<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var values = ExtractDictionary(parameters, "values");
        var where = ExtractDictionary(parameters, "where");

        if (values == null || values.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_VALUES_REQUIRED", ErrorMessage: "Update operation requires 'values' dictionary.");
        }

        if (where == null || where.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_FILTER_REQUIRED", ErrorMessage: "Update operation requires 'where' filter dictionary.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        var setClauses = new List<string>();
        int idx = 0;

        foreach (var kv in values)
        {
            string exactCol = ResolveValidatedColumn(kv.Key, validColumns);
            string pName = $"@s{idx++}";
            setClauses.Add($"{QuoteIdentifier(exactCol)} = {pName}");
            AddParameter(cmd, pName, kv.Value);
        }

        var whereClauses = BuildParameterizedWhereClauses(where, validColumns, cmd, "w");
        cmd.CommandText = $"UPDATE {quotedTable} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)} RETURNING *";

        var rows = await ReadRowsAsync(cmd, ct);
        return new ServiceBrokerInvocationResult(true, Data: rows.FirstOrDefault(), Rows: rows, AffectedRows: rows.Count);
    }

    private static async Task<ServiceBrokerInvocationResult> ExecuteDeleteAsync(
        DbConnection conn,
        string quotedTable,
        HashSet<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var where = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);
        if (where.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "POSTGRES_FILTER_REQUIRED", ErrorMessage: "Delete operation requires a 'where' filter.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        var whereClauses = BuildParameterizedWhereClauses(where, validColumns, cmd, "w");
        cmd.CommandText = $"DELETE FROM {quotedTable} WHERE {string.Join(" AND ", whereClauses)}";

        int affected = await cmd.ExecuteNonQueryAsync(ct);
        return new ServiceBrokerInvocationResult(true, AffectedRows: affected);
    }

    private static List<string> BuildParameterizedWhereClauses(
        IReadOnlyDictionary<string, object?> filters,
        HashSet<string> validColumns,
        DbCommand cmd,
        string prefix)
    {
        var clauses = new List<string>();
        int idx = 0;

        foreach (var kv in filters)
        {
            string exactCol = ResolveValidatedColumn(kv.Key, validColumns);
            object? unwrapped = UnwrapJsonValue(kv.Value);
            if (unwrapped == null)
            {
                clauses.Add($"{QuoteIdentifier(exactCol)} IS NULL");
            }
            else
            {
                string pName = $"@{prefix}{idx++}";
                clauses.Add($"{QuoteIdentifier(exactCol)} = {pName}");
                AddParameter(cmd, pName, unwrapped);
            }
        }

        return clauses;
    }

    private static string ResolveValidatedColumn(string requestedColumn, HashSet<string> validColumns)
    {
        if (string.IsNullOrWhiteSpace(requestedColumn))
        {
            throw new ArgumentException("Column name cannot be empty.");
        }

        string trimmed = requestedColumn.Trim();
        if (validColumns.Contains(trimmed))
        {
            return trimmed;
        }

        string? caseInsensitiveMatch = validColumns.FirstOrDefault(c =>
            string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitiveMatch != null)
        {
            return caseInsensitiveMatch;
        }

        throw new ArgumentException($"Column '{requestedColumn}' is not a valid column in the target table.");
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        object? unwrapped = UnwrapJsonValue(value);
        if (cmd is NpgsqlCommand npgCmd)
        {
            npgCmd.Parameters.Add(new NpgsqlParameter(name, unwrapped ?? DBNull.Value));
            return;
        }

        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = unwrapped ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }

    private static object? UnwrapJsonValue(object? value)
    {
        if (value is not JsonElement el)
        {
            return value;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.TryGetInt32(out int i) ? i
                : el.TryGetInt64(out long l) ? l
                : el.GetDecimal(),
            _ => el.GetRawText()
        };
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(DbCommand cmd, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, object?>? ExtractDictionary(IReadOnlyDictionary<string, object?> parameters, string key)
    {
        var match = parameters.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Key == null || match.Value == null)
        {
            return null;
        }

        if (match.Value is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (match.Value is IReadOnlyDictionary<string, object?> roDict)
        {
            return new Dictionary<string, object?>(roDict, StringComparer.OrdinalIgnoreCase);
        }

        if (match.Value is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in el.EnumerateObject())
            {
                result[prop.Name] = UnwrapJsonValue(prop.Value);
            }
            return result;
        }

        return null;
    }

    private static Dictionary<string, object?> ExtractNonReservedParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "limit", "offset", "where", "values" };
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parameters)
        {
            if (!reserved.Contains(kv.Key))
            {
                result[kv.Key] = UnwrapJsonValue(kv.Value);
            }
        }
        return result;
    }

    private static int ExtractInt(IReadOnlyDictionary<string, object?> parameters, string key, int defaultValue, int min, int max)
    {
        var match = parameters.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Key == null || match.Value == null)
        {
            return defaultValue;
        }

        object? unwrapped = UnwrapJsonValue(match.Value);
        if (unwrapped != null && int.TryParse(unwrapped.ToString(), out int parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        return defaultValue;
    }

    private static bool TryBuildConnectionString(
        Connection connection,
        ResolvedExecutionSecret? secret,
        int timeoutSeconds,
        out string? connectionString,
        out string? error)
    {
        connectionString = null;
        error = null;

        try
        {
            string? explicitConnStr = secret?.GetField("connectionString", "connection_string");
            if (string.IsNullOrWhiteSpace(explicitConnStr) &&
                secret != null &&
                !secret.RawValue.TrimStart().StartsWith('{') &&
                secret.RawValue.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                explicitConnStr = secret.RawValue;
            }

            NpgsqlConnectionStringBuilder builder;
            if (!string.IsNullOrWhiteSpace(explicitConnStr))
            {
                builder = new NpgsqlConnectionStringBuilder(explicitConnStr);
            }
            else
            {
                builder = new NpgsqlConnectionStringBuilder();

                if (!string.IsNullOrWhiteSpace(connection.BaseUrl) &&
                    Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
                {
                    builder.Host = uri.Host;
                    if (uri.Port > 0) builder.Port = uri.Port;
                    string db = uri.AbsolutePath.Trim('/');
                    if (!string.IsNullOrWhiteSpace(db)) builder.Database = db;
                }

                string? host = secret?.GetField("host", "server");
                if (!string.IsNullOrWhiteSpace(host)) builder.Host = host;

                string? portStr = secret?.GetField("port");
                if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out int port)) builder.Port = port;

                string? database = secret?.GetField("database", "db");
                if (!string.IsNullOrWhiteSpace(database)) builder.Database = database;

                string? username = secret?.GetField("username", "user");
                if (!string.IsNullOrWhiteSpace(username)) builder.Username = username;

                string? password = secret?.GetField("password", "pwd");
                if (!string.IsNullOrWhiteSpace(password)) builder.Password = password;
            }

            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                error = "PostgreSQL connection is missing required Host configuration.";
                return false;
            }

            builder.Timeout = timeoutSeconds;
            builder.CommandTimeout = timeoutSeconds;
            connectionString = builder.ConnectionString;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid PostgreSQL connection configuration: {ex.Message}";
            return false;
        }
    }
}
