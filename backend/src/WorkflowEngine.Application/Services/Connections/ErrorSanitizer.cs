using System.Text.RegularExpressions;

namespace WorkflowEngine.Application.Services.Connections;

/// <summary>
/// Sanitizes diagnostic and runtime error messages so passwords, tokens, API keys,
/// and raw database connection strings can never leak to API responses, logs, or UI.
/// </summary>
public static partial class ErrorSanitizer
{
    [GeneratedRegex(@"(?i)(password|pwd|secret|token|apikey|api_key|access_token|client_secret)\s*=\s*[^;,\s""']+", RegexOptions.Compiled)]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"(?i)(postgres(?:ql)?|mssql|sqlserver|https?)://[^:\s]+:[^@\s]+@", RegexOptions.Compiled)]
    private static partial Regex UriCredentialsRegex();

    [GeneratedRegex(@"(?i)(Bearer|Basic)\s+[A-Za-z0-9\-._~+/=]+", RegexOptions.Compiled)]
    private static partial Regex AuthHeaderRegex();

    [GeneratedRegex(@"(?i)(Host|Server|Data Source)\s*=\s*[^;]+;\s*(Port|Database|Initial Catalog|Username|User Id|UID|Password|PWD)\s*=.*", RegexOptions.Compiled)]
    private static partial Regex ConnectionStringDumpRegex();

    public static string Sanitize(string? message, IEnumerable<string>? knownSecrets = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Connection error occurred.";
        }

        string sanitized = message;

        if (knownSecrets != null)
        {
            foreach (var secret in knownSecrets)
            {
                if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= 3)
                {
                    sanitized = sanitized.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
                }
            }
        }

        sanitized = UriCredentialsRegex().Replace(sanitized, "$1://[REDACTED]@");
        sanitized = AuthHeaderRegex().Replace(sanitized, "$1 [REDACTED]");
        sanitized = KeyValueSecretRegex().Replace(sanitized, "$1=[REDACTED]");
        sanitized = ConnectionStringDumpRegex().Replace(sanitized, "[REDACTED_CONNECTION_STRING]");

        if (sanitized.Length > 500)
        {
            sanitized = sanitized[..500] + "...";
        }

        return sanitized;
    }
}
