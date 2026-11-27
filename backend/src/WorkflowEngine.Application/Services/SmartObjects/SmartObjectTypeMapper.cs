using System.Text.RegularExpressions;

namespace WorkflowEngine.Application.Services.SmartObjects;

/// <summary>
/// Documented source-to-SmartObject type mapper for SQL (PostgreSQL, SQL Server) and SharePoint Graph schemas.
/// Explicit mapping table:
/// - int, bigint, smallint, decimal, numeric, float, real, Number, Currency -> "number"
/// - bit, boolean, YesNo -> "boolean"
/// - date, datetime, datetime2, timestamp, timestamptz, DateTime -> "datetime"
/// - json, jsonb -> "json"
/// - Any other or unknown type (varchar, nvarchar, text, uuid, Choice, Person, etc.) -> explicit fallback "string"
/// </summary>
public static partial class SmartObjectTypeMapper
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Numeric types -> "number"
        ["int"] = "number",
        ["int2"] = "number",
        ["int4"] = "number",
        ["int8"] = "number",
        ["integer"] = "number",
        ["bigint"] = "number",
        ["smallint"] = "number",
        ["tinyint"] = "number",
        ["decimal"] = "number",
        ["numeric"] = "number",
        ["float"] = "number",
        ["float4"] = "number",
        ["float8"] = "number",
        ["double"] = "number",
        ["double precision"] = "number",
        ["real"] = "number",
        ["money"] = "number",
        ["smallmoney"] = "number",
        ["number"] = "number",
        ["currency"] = "number",

        // Boolean types -> "boolean"
        ["bit"] = "boolean",
        ["bool"] = "boolean",
        ["boolean"] = "boolean",
        ["yesno"] = "boolean",

        // Date / time types -> "datetime"
        ["date"] = "datetime",
        ["datetime"] = "datetime",
        ["datetime2"] = "datetime",
        ["smalldatetime"] = "datetime",
        ["datetimeoffset"] = "datetime",
        ["timestamp"] = "datetime",
        ["timestamptz"] = "datetime",
        ["timestamp without time zone"] = "datetime",
        ["timestamp with time zone"] = "datetime",

        // Structured JSON -> "json"
        ["json"] = "json",
        ["jsonb"] = "json"
    };

    private static readonly string[] SensitiveFieldKeywords =
    {
        "password",
        "secret",
        "token",
        "iban",
        "ssn",
        "salary"
    };

    /// <summary>
    /// Maps a source database or SharePoint column type to its canonical SmartObject type.
    /// Falls back explicitly to "string" for all unmapped or unknown types.
    /// </summary>
    public static string MapToSmartObjectType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return "string";
        }

        string normalized = sourceType.Trim();

        // Strip length/precision modifiers such as "varchar(255)" or "numeric(18,2)"
        int parenIndex = normalized.IndexOf('(');
        if (parenIndex > 0)
        {
            normalized = normalized[..parenIndex].Trim();
        }

        return TypeMap.TryGetValue(normalized, out var mapped)
            ? mapped
            : "string";
    }

    /// <summary>
    /// Returns true if the column name suggests sensitive data that should be masked by default.
    /// </summary>
    public static bool IsSuggestedMasked(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        foreach (var keyword in SensitiveFieldKeywords)
        {
            if (fieldName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Produces a human-friendly display name from a snake_case or camelCase identifier.
    /// </summary>
    public static string FormatDisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Entity";
        }

        string withoutSchema = rawName.Contains('.')
            ? rawName[(rawName.LastIndexOf('.') + 1)..]
            : rawName;

        string spaced = withoutSchema.Replace('_', ' ').Replace('-', ' ');
        spaced = CamelCaseRegex().Replace(spaced, "$1 $2");
        spaced = MultiSpaceRegex().Replace(spaced, " ").Trim();

        if (spaced.Length == 0)
        {
            return rawName;
        }

        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();
}
