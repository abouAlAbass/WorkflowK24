using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.SmartObjects;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Services.Brokers;

public sealed record SqlServerQueryResult(
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int AffectedRows,
    bool ResponseSizeLimitExceeded = false);

/// <summary>
/// Testable abstraction over Microsoft.Data.SqlClient command execution.
/// Production uses <see cref="SqlClientCommandRunner"/>; tests can substitute a lightweight fake runner.
/// </summary>
public interface ISqlServerCommandRunner
{
    Task<object?> ExecuteScalarAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default);

    Task<SqlServerQueryResult> ExecuteQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        int maxResponseBytes,
        CancellationToken ct = default);

    Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default);
}

public sealed class SqlClientCommandRunner : ISqlServerCommandRunner
{
    public async Task<object?> ExecuteScalarAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = commandTimeoutSeconds;
        AttachParameters(cmd, parameters);

        return await cmd.ExecuteScalarAsync(ct);
    }

    public async Task<SqlServerQueryResult> ExecuteQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        int maxResponseBytes,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = commandTimeoutSeconds;
        AttachParameters(cmd, parameters);

        var rows = new List<Dictionary<string, object?>>();
        long approxBytes = 2; // JSON array brackets []

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string colName = reader.GetName(i);
                object? val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[colName] = val;
            }

            approxBytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row)) + 1;
            if (approxBytes > maxResponseBytes)
            {
                return new SqlServerQueryResult(Array.Empty<Dictionary<string, object?>>(), 0, ResponseSizeLimitExceeded: true);
            }

            rows.Add(row);
        }

        return new SqlServerQueryResult(rows, rows.Count);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = commandTimeoutSeconds;
        AttachParameters(cmd, parameters);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AttachParameters(SqlCommand cmd, IReadOnlyList<SqlParameter> parameters)
    {
        foreach (var p in parameters)
        {
            cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value ?? DBNull.Value));
        }
    }
}

/// <summary>
/// SQL Server Service Broker backed by Microsoft.Data.SqlClient (via ISqlServerCommandRunner).
/// Enforces:
/// - Read-only by default (AllowWrite = false blocks Create/Update/Delete with 403 WRITE_NOT_ALLOWED_ON_CONNECTION).
/// - Strict schema/table/column whitelisting via sys.tables/sys.columns (excluding sys and INFORMATION_SCHEMA).
/// - Hard row limit (&lt;= connection.MaxRows, absolute cap 1000) and response size limit (1 MiB).
/// - Parameterized SqlParameter queries only, with CommandTimeout = 15s (5s for TestConnectionAsync).
/// </summary>
public class SqlServerServiceBroker : IServiceBroker
{
    public const int TestTimeoutSeconds = 5;
    public const int CommandTimeoutSeconds = 15;
    public const int HardMaxRows = 1000;
    public const int MaxResponseBytes = 1024 * 1024; // 1 MiB

    private static readonly HashSet<string> ForbiddenSchemas = new(StringComparer.OrdinalIgnoreCase)
    {
        "sys",
        "INFORMATION_SCHEMA"
    };

    private static readonly HashSet<string> WriteOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "create",
        "update",
        "delete"
    };

    private readonly ISqlServerCommandRunner _runner;

    public SqlServerServiceBroker(ISqlServerCommandRunner? runner = null)
    {
        _runner = runner ?? new SqlClientCommandRunner();
    }

    public string ServiceType => "sqlserver";

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

            var scalar = await _runner.ExecuteScalarAsync(
                connString!,
                "SELECT 1",
                Array.Empty<SqlParameter>(),
                TestTimeoutSeconds,
                timeoutCts.Token);

            sw.Stop();
            bool ok = scalar != null && Convert.ToInt32(scalar) == 1;
            return ok
                ? new ConnectionTestResult(true, sw.ElapsedMilliseconds)
                : new ConnectionTestResult(false, sw.ElapsedMilliseconds, "Unexpected response from SQL Server health check.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, $"SQL Server connection test timed out after {TestTimeoutSeconds}s.");
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

        const string sql = """
            SELECT
                s.name AS schema_name,
                o.name AS entity_name,
                o.entity_type AS entity_type,
                c.name AS column_name,
                tp.name AS data_type,
                c.is_nullable AS is_nullable,
                CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key
            FROM (
                SELECT object_id, schema_id, name, 'Table' AS entity_type FROM sys.tables
                UNION ALL
                SELECT object_id, schema_id, name, 'View' AS entity_type FROM sys.views
            ) o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.columns c ON o.object_id = c.object_id
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic
                    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_primary_key = 1
            ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
            WHERE s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
            ORDER BY s.name, o.name, c.column_id
            """;

        try
        {
            var result = await _runner.ExecuteQueryAsync(
                connString!,
                sql,
                Array.Empty<SqlParameter>(),
                CommandTimeoutSeconds,
                MaxResponseBytes,
                ct);

            var grouped = new Dictionary<string, (string Schema, string Name, string EntityType, List<DiscoveredFieldDto> Fields)>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (var row in result.Rows)
            {
                string schema = GetRowString(row, "schema_name") ?? "dbo";
                if (ForbiddenSchemas.Contains(schema)) continue;

                string entityNameRaw = GetRowString(row, "entity_name") ?? string.Empty;
                string colName = GetRowString(row, "column_name") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entityNameRaw) || string.IsNullOrWhiteSpace(colName)) continue;

                string entityType = GetRowString(row, "entity_type") ?? "Table";
                string dataType = GetRowString(row, "data_type") ?? "nvarchar";
                bool isNullable = GetRowBool(row, "is_nullable", defaultValue: true);
                bool isPk = GetRowBool(row, "is_primary_key", defaultValue: false);

                string key = $"{schema}.{entityNameRaw}";
                if (!grouped.TryGetValue(key, out var entry))
                {
                    entry = (schema, entityNameRaw, entityType, new List<DiscoveredFieldDto>());
                    grouped[key] = entry;
                    order.Add(key);
                }

                entry.Fields.Add(new DiscoveredFieldDto(
                    Name: colName,
                    SourceType: dataType,
                    MappedSmartObjectType: SmartObjectTypeMapper.MapToSmartObjectType(dataType),
                    IsNullable: isNullable,
                    IsPrimaryKey: isPk,
                    SuggestedMasked: SmartObjectTypeMapper.IsSuggestedMasked(colName)));
            }

            var entities = new List<DiscoveredEntityDto>(order.Count);
            foreach (var key in order)
            {
                var (schema, name, entityType, fields) = grouped[key];
                string entityName = string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : $"{schema}.{name}";
                string? detectedPk = fields.FirstOrDefault(f => f.IsPrimaryKey)?.Name;

                entities.Add(new DiscoveredEntityDto(
                    EntityName: entityName,
                    DisplayName: SmartObjectTypeMapper.FormatDisplayName(name),
                    DetectedPrimaryKey: detectedPk,
                    Fields: fields,
                    SchemaName: schema,
                    EntityType: entityType));
            }

            return entities;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(ex.Message, allSecrets), ex);
        }
    }

    private static string? GetRowString(Dictionary<string, object?> row, string key)
    {
        var match = row.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        return match.Key != null ? match.Value?.ToString() : null;
    }

    private static bool GetRowBool(Dictionary<string, object?> row, string key, bool defaultValue)
    {
        var match = row.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Key == null || match.Value == null) return defaultValue;
        if (match.Value is bool b) return b;
        if (match.Value is int i) return i != 0;
        if (match.Value is long l) return l != 0;
        string s = match.Value.ToString() ?? string.Empty;
        if (bool.TryParse(s, out bool parsed)) return parsed;
        if (int.TryParse(s, out int parsedInt)) return parsedInt != 0;
        return defaultValue;
    }

    public async Task<ServiceBrokerInvocationResult> InvokeAsync(
        ServiceBrokerInvocationRequest request,
        CancellationToken ct = default)
    {
        var connection = request.Connection;
        var secretsToRedact = request.Secret?.SecretsToRedact ?? Array.Empty<string>();
        string operation = request.Operation?.Trim().ToLowerInvariant() ?? "list";

        // 1. Strict Read-Only Guard (AllowWrite defaults to false)
        if (WriteOperations.Contains(operation) && !connection.AllowWrite)
        {
            throw new ConnectionWriteNotAllowedException(
                connection.Id,
                request.Operation ?? operation,
                $"Write operation '{request.Operation}' is forbidden on SQL Server connection '{connection.Name}' (AllowWrite = false).");
        }

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SQLSERVER_TABLE_REQUIRED",
                ErrorMessage: "TableName is required for SQL Server broker operations.");
        }

        string schemaName = string.IsNullOrWhiteSpace(request.SchemaName) ? "dbo" : request.SchemaName.Trim();
        string tableName = request.TableName.Trim();

        if (ForbiddenSchemas.Contains(schemaName))
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SQLSERVER_FORBIDDEN_SCHEMA",
                ErrorMessage: $"Schema '{schemaName}' is forbidden for SQL Server broker operations.");
        }

        if (!TryBuildConnectionString(connection, request.Secret, CommandTimeoutSeconds, out var connString, out var buildError))
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SQLSERVER_CONFIG_INVALID",
                ErrorMessage: ErrorSanitizer.Sanitize(buildError, secretsToRedact));
        }

        var allSecrets = new List<string>(secretsToRedact) { connString! };
        int cmdTimeout = Math.Clamp(request.TimeoutSeconds ?? CommandTimeoutSeconds, 1, 60);
        int effectiveMaxRows = Math.Clamp(connection.MaxRows, 1, HardMaxRows);

        try
        {
            // 2. Validate table & load whitelisted columns from sys.tables / sys.columns (excluding sys & INFORMATION_SCHEMA)
            var validColumns = await LoadTableColumnsAsync(connString!, schemaName, tableName, cmdTimeout, ct);
            if (validColumns.Count == 0)
            {
                return new ServiceBrokerInvocationResult(
                    false,
                    ErrorCode: "SQLSERVER_INVALID_TABLE",
                    ErrorMessage: $"Table '{schemaName}.{tableName}' does not exist or is not accessible.");
            }

            string quotedTable = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
            var parameters = request.Parameters ?? new Dictionary<string, object?>();

            return operation switch
            {
                "get" => await ExecuteGetAsync(connString!, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "list" => await ExecuteListAsync(connString!, quotedTable, validColumns, parameters, effectiveMaxRows, cmdTimeout, ct),
                "create" => await ExecuteCreateAsync(connString!, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "update" => await ExecuteUpdateAsync(connString!, quotedTable, validColumns, parameters, cmdTimeout, ct),
                "delete" => await ExecuteDeleteAsync(connString!, quotedTable, validColumns, parameters, cmdTimeout, ct),
                _ => new ServiceBrokerInvocationResult(
                    false,
                    ErrorCode: "SQLSERVER_UNSUPPORTED_OPERATION",
                    ErrorMessage: $"Unsupported SQL Server operation '{request.Operation}'. Allowed: Get, List, Create, Update, Delete.")
            };
        }
        catch (ConnectionWriteNotAllowedException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_INVALID_IDENTIFIER", ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SQLSERVER_EXECUTION_ERROR",
                ErrorMessage: ErrorSanitizer.Sanitize(ex.Message, allSecrets));
        }
    }

    private async Task<List<string>> LoadTableColumnsAsync(
        string connectionString,
        string schemaName,
        string tableName,
        int commandTimeout,
        CancellationToken ct)
    {
        const string sql = """
            SELECT c.name AS column_name
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema
              AND t.name = @table
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
            ORDER BY c.column_id
            """;

        var sqlParams = new List<SqlParameter>
        {
            new("@schema", schemaName),
            new("@table", tableName)
        };

        var result = await _runner.ExecuteQueryAsync(connectionString, sql, sqlParams, commandTimeout, MaxResponseBytes, ct);
        var columns = new List<string>();
        foreach (var row in result.Rows)
        {
            if (row.TryGetValue("column_name", out var val) && val != null)
            {
                string col = val.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(col))
                {
                    columns.Add(col);
                }
            }
            else if (row.Count > 0)
            {
                string col = row.Values.First()?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(col))
                {
                    columns.Add(col);
                }
            }
        }

        return columns;
    }

    private async Task<ServiceBrokerInvocationResult> ExecuteGetAsync(
        string connectionString,
        string quotedTable,
        List<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var filters = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);
        if (filters.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_FILTER_REQUIRED", ErrorMessage: "Get operation requires at least one filter column/value.");
        }

        var sqlParams = new List<SqlParameter>();
        var whereClauses = BuildParameterizedWhereClauses(filters, validColumns, sqlParams, "w");
        string sql = $"SELECT TOP (1) * FROM {quotedTable} WHERE {string.Join(" AND ", whereClauses)}";

        var queryResult = await _runner.ExecuteQueryAsync(connectionString, sql, sqlParams, commandTimeout, MaxResponseBytes, ct);
        if (queryResult.ResponseSizeLimitExceeded)
        {
            return ResponseTooLargeResult();
        }

        return new ServiceBrokerInvocationResult(true, Data: queryResult.Rows.FirstOrDefault(), Rows: queryResult.Rows, AffectedRows: queryResult.Rows.Count);
    }

    private async Task<ServiceBrokerInvocationResult> ExecuteListAsync(
        string connectionString,
        string quotedTable,
        List<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int effectiveMaxRows,
        int commandTimeout,
        CancellationToken ct)
    {
        var filters = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);

        int requestedPageSize = HasKey(parameters, "pageSize")
            ? ExtractInt(parameters, "pageSize", effectiveMaxRows, 1, HardMaxRows)
            : ExtractInt(parameters, "limit", effectiveMaxRows, 1, HardMaxRows);

        int pageSize = Math.Clamp(Math.Min(requestedPageSize, effectiveMaxRows), 1, HardMaxRows);
        int page = ExtractInt(parameters, "page", 1, 1, 100_000);
        int offset = HasKey(parameters, "offset")
            ? ExtractInt(parameters, "offset", 0, 0, 10_000_000)
            : (page - 1) * pageSize;

        var sqlParams = new List<SqlParameter>();
        string whereSql = string.Empty;
        if (filters.Count > 0)
        {
            var whereClauses = BuildParameterizedWhereClauses(filters, validColumns, sqlParams, "w");
            whereSql = $" WHERE {string.Join(" AND ", whereClauses)}";
        }

        string sql;
        if (offset > 0)
        {
            string orderCol = QuoteIdentifier(validColumns[0]);
            sqlParams.Add(new SqlParameter("@offset", offset));
            sqlParams.Add(new SqlParameter("@pageSize", pageSize));
            sql = $"SELECT * FROM {quotedTable}{whereSql} ORDER BY {orderCol} OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
        }
        else
        {
            sqlParams.Add(new SqlParameter("@maxRows", pageSize));
            sql = $"SELECT TOP (@maxRows) * FROM {quotedTable}{whereSql}";
        }

        var queryResult = await _runner.ExecuteQueryAsync(connectionString, sql, sqlParams, commandTimeout, MaxResponseBytes, ct);
        if (queryResult.ResponseSizeLimitExceeded)
        {
            return ResponseTooLargeResult();
        }

        // Belt-and-suspenders cap in case a custom runner returns more rows than requested
        var boundedRows = queryResult.Rows.Count > pageSize
            ? queryResult.Rows.Take(pageSize).ToList()
            : queryResult.Rows;

        return new ServiceBrokerInvocationResult(true, Data: boundedRows, Rows: boundedRows, AffectedRows: boundedRows.Count);
    }

    private async Task<ServiceBrokerInvocationResult> ExecuteCreateAsync(
        string connectionString,
        string quotedTable,
        List<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var values = ExtractDictionary(parameters, "values") ?? ExtractNonReservedParameters(parameters);
        if (values.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_VALUES_REQUIRED", ErrorMessage: "Create operation requires at least one column value.");
        }

        var cols = new List<string>();
        var paramNames = new List<string>();
        var sqlParams = new List<SqlParameter>();
        int idx = 0;

        foreach (var kv in values)
        {
            string exactCol = ResolveValidatedColumn(kv.Key, validColumns);
            string pName = $"@v{idx++}";
            cols.Add(QuoteIdentifier(exactCol));
            paramNames.Add(pName);
            sqlParams.Add(new SqlParameter(pName, UnwrapJsonValue(kv.Value) ?? DBNull.Value));
        }

        string sql = $"INSERT INTO {quotedTable} ({string.Join(", ", cols)}) OUTPUT INSERTED.* VALUES ({string.Join(", ", paramNames)})";
        var queryResult = await _runner.ExecuteQueryAsync(connectionString, sql, sqlParams, commandTimeout, MaxResponseBytes, ct);
        if (queryResult.ResponseSizeLimitExceeded)
        {
            return ResponseTooLargeResult();
        }

        return new ServiceBrokerInvocationResult(true, Data: queryResult.Rows.FirstOrDefault(), Rows: queryResult.Rows, AffectedRows: queryResult.Rows.Count);
    }

    private async Task<ServiceBrokerInvocationResult> ExecuteUpdateAsync(
        string connectionString,
        string quotedTable,
        List<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var values = ExtractDictionary(parameters, "values");
        var where = ExtractDictionary(parameters, "where");

        if (values == null || values.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_VALUES_REQUIRED", ErrorMessage: "Update operation requires 'values' dictionary.");
        }

        if (where == null || where.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_FILTER_REQUIRED", ErrorMessage: "Update operation requires 'where' filter dictionary.");
        }

        var setClauses = new List<string>();
        var sqlParams = new List<SqlParameter>();
        int idx = 0;

        foreach (var kv in values)
        {
            string exactCol = ResolveValidatedColumn(kv.Key, validColumns);
            string pName = $"@s{idx++}";
            setClauses.Add($"{QuoteIdentifier(exactCol)} = {pName}");
            sqlParams.Add(new SqlParameter(pName, UnwrapJsonValue(kv.Value) ?? DBNull.Value));
        }

        var whereClauses = BuildParameterizedWhereClauses(where, validColumns, sqlParams, "w");
        string sql = $"UPDATE {quotedTable} SET {string.Join(", ", setClauses)} OUTPUT INSERTED.* WHERE {string.Join(" AND ", whereClauses)}";

        var queryResult = await _runner.ExecuteQueryAsync(connectionString, sql, sqlParams, commandTimeout, MaxResponseBytes, ct);
        if (queryResult.ResponseSizeLimitExceeded)
        {
            return ResponseTooLargeResult();
        }

        return new ServiceBrokerInvocationResult(true, Data: queryResult.Rows.FirstOrDefault(), Rows: queryResult.Rows, AffectedRows: queryResult.Rows.Count);
    }

    private async Task<ServiceBrokerInvocationResult> ExecuteDeleteAsync(
        string connectionString,
        string quotedTable,
        List<string> validColumns,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeout,
        CancellationToken ct)
    {
        var where = ExtractDictionary(parameters, "where") ?? ExtractNonReservedParameters(parameters);
        if (where.Count == 0)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SQLSERVER_FILTER_REQUIRED", ErrorMessage: "Delete operation requires a 'where' filter.");
        }

        var sqlParams = new List<SqlParameter>();
        var whereClauses = BuildParameterizedWhereClauses(where, validColumns, sqlParams, "w");
        string sql = $"DELETE FROM {quotedTable} WHERE {string.Join(" AND ", whereClauses)}";

        int affected = await _runner.ExecuteNonQueryAsync(connectionString, sql, sqlParams, commandTimeout, ct);
        return new ServiceBrokerInvocationResult(true, AffectedRows: affected);
    }

    private static ServiceBrokerInvocationResult ResponseTooLargeResult() =>
        new(
            false,
            ErrorCode: "SQLSERVER_RESPONSE_TOO_LARGE",
            ErrorMessage: $"SQL Server query response exceeded the maximum allowed size of {MaxResponseBytes} bytes (1 MiB).");

    private static List<string> BuildParameterizedWhereClauses(
        IReadOnlyDictionary<string, object?> filters,
        List<string> validColumns,
        List<SqlParameter> sqlParams,
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
                sqlParams.Add(new SqlParameter(pName, unwrapped));
            }
        }

        return clauses;
    }

    private static string ResolveValidatedColumn(string requestedColumn, List<string> validColumns)
    {
        if (string.IsNullOrWhiteSpace(requestedColumn))
        {
            throw new ArgumentException("Column name cannot be empty.");
        }

        string trimmed = requestedColumn.Trim();
        string? exact = validColumns.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.Ordinal))
            ?? validColumns.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            return exact;
        }

        throw new ArgumentException($"Column '{requestedColumn}' is not a valid column in the target table.");
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
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
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "limit", "offset", "page", "pageSize", "where", "values"
        };
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

    private static bool HasKey(IReadOnlyDictionary<string, object?> parameters, string key) =>
        parameters.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

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
        int connectTimeoutSeconds,
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
                (secret.RawValue.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                 secret.RawValue.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)))
            {
                explicitConnStr = secret.RawValue;
            }

            SqlConnectionStringBuilder builder;
            if (!string.IsNullOrWhiteSpace(explicitConnStr))
            {
                builder = new SqlConnectionStringBuilder(explicitConnStr);
            }
            else
            {
                builder = new SqlConnectionStringBuilder();

                if (!string.IsNullOrWhiteSpace(connection.BaseUrl) &&
                    Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme.Equals("sqlserver", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("mssql", StringComparison.OrdinalIgnoreCase)))
                {
                    builder.DataSource = uri.Port > 0 ? $"{uri.Host},{uri.Port}" : uri.Host;
                    string db = uri.AbsolutePath.Trim('/');
                    if (!string.IsNullOrWhiteSpace(db)) builder.InitialCatalog = db;
                }

                string? server = secret?.GetField("server", "host", "dataSource");
                if (!string.IsNullOrWhiteSpace(server))
                {
                    string? portStr = secret?.GetField("port");
                    builder.DataSource = !string.IsNullOrWhiteSpace(portStr) && !server.Contains(',')
                        ? $"{server},{portStr}"
                        : server;
                }

                string? database = secret?.GetField("database", "db", "initialCatalog");
                if (!string.IsNullOrWhiteSpace(database)) builder.InitialCatalog = database;

                string? username = secret?.GetField("username", "user", "userId");
                if (!string.IsNullOrWhiteSpace(username)) builder.UserID = username;

                string? password = secret?.GetField("password", "pwd");
                if (!string.IsNullOrWhiteSpace(password)) builder.Password = password;
            }

            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                error = "SQL Server connection is missing required Server/DataSource configuration.";
                return false;
            }

            builder.ConnectTimeout = connectTimeoutSeconds;
            connectionString = builder.ConnectionString;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid SQL Server connection configuration: {ex.Message}";
            return false;
        }
    }
}
