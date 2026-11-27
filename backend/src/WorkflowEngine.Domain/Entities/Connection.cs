using System.Text.Json;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Reusable external service connection separating workflow definitions from endpoints and credentials.
/// Supports multiple service brokers (rest, postgres, ...) and secret sources (internal_encrypted, external_vault, user_sso_vault).
/// </summary>
public class Connection
{
    public static readonly IReadOnlySet<string> AllowedAuthTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "basic",
        "bearer",
        "api_key",
        "oauth2_client_credentials"
    };

    public static readonly IReadOnlySet<string> AllowedServiceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "rest",
        "postgres",
        "sqlserver",
        "sharepoint"
    };

    public static readonly IReadOnlySet<string> AllowedAuthModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "static",
        "oauth2_client_credentials",
        "user_sso_vault",
        "on_behalf_of"
    };

    public static readonly IReadOnlySet<string> AllowedHealthStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Unknown",
        "Healthy",
        "Degraded",
        "Unreachable"
    };

    public static readonly IReadOnlySet<string> AllowedSecretSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "internal_encrypted",
        "external_vault"
    };

    public const int DefaultMaxRows = 200;
    public const int MinMaxRows = 1;
    public const int MaxAllowedRows = 1000;

    public string Id { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string ServiceType { get; private set; } = "rest";
    public string BaseUrl { get; private set; } = string.Empty;
    public string AuthType { get; private set; } = "none";
    public string AuthMode { get; private set; } = "static";
    public string SecretSource { get; private set; } = "internal_encrypted";
    public string? SecretRef { get; private set; }
    public string? EncryptedCredentialsJson { get; private set; }
    public string? ExternalSecretKey { get; private set; }
    public string AllowedHostsJson { get; private set; } = "[]";
    public string? DefaultHeadersJson { get; private set; }
    public bool AllowWrite { get; private set; } = false;
    public int MaxRows { get; private set; } = DefaultMaxRows;
    public DateTime? SecretExpiresAt { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }
    public string HealthStatus { get; private set; } = "Unknown";
    public DateTime? LastTestedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public string? LastErrorSanitized { get; private set; }
    public int Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private readonly List<ConnectionPermission> _permissions = new();
    public IReadOnlyCollection<ConnectionPermission> Permissions => _permissions;

    private readonly List<UserConnectionCredential> _userCredentials = new();
    public IReadOnlyCollection<UserConnectionCredential> UserCredentials => _userCredentials;

    private readonly List<ConnectionEnvironmentConfig> _environmentConfigs = new();
    public IReadOnlyCollection<ConnectionEnvironmentConfig> EnvironmentConfigs => _environmentConfigs;

    private readonly List<ConnectionUsageLog> _usageLogs = new();
    public IReadOnlyCollection<ConnectionUsageLog> UsageLogs => _usageLogs;

    private Connection() { } // Required for ORM hydration

    public Connection(
        string id,
        string name,
        string baseUrl,
        string authType,
        string? secretRef,
        string allowedHostsJson,
        string? defaultHeadersJson,
        string createdBy,
        string serviceType = "rest",
        string authMode = "static",
        string secretSource = "internal_encrypted",
        string? encryptedCredentialsJson = null,
        string? externalSecretKey = null,
        bool allowWrite = false,
        int maxRows = DefaultMaxRows,
        DateTime? secretExpiresAt = null)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? $"conn-{Guid.NewGuid():N}"[..20]
            : id.Trim();
        Name = ValidateName(name);
        ServiceType = NormalizeAndValidateServiceType(serviceType);
        BaseUrl = ValidateBaseUrl(baseUrl, ServiceType);
        AuthType = NormalizeAndValidateAuthType(authType);
        AuthMode = NormalizeAndValidateAuthMode(authMode);
        SecretSource = NormalizeAndValidateSecretSource(secretSource);
        SecretRef = NormalizeOptional(secretRef);
        EncryptedCredentialsJson = NormalizeOptional(encryptedCredentialsJson);
        ExternalSecretKey = NormalizeOptional(externalSecretKey);
        AllowedHostsJson = NormalizeAllowedHostsJson(allowedHostsJson);
        DefaultHeadersJson = NormalizeDefaultHeadersJson(defaultHeadersJson);
        AllowWrite = allowWrite;
        MaxRows = ValidateMaxRows(maxRows);
        SecretExpiresAt = secretExpiresAt;
        HealthStatus = "Unknown";
        CreatedBy = string.IsNullOrWhiteSpace(createdBy)
            ? throw new ArgumentException("CreatedBy is required.", nameof(createdBy))
            : createdBy.Trim();
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateSecret(
        string? encryptedCredentialsJson,
        bool updateEncryptedCredentials,
        string? externalSecretKey,
        bool updateExternalSecretKey,
        DateTime? secretExpiresAt,
        DateTime? now = null)
    {
        var utcNow = now ?? DateTime.UtcNow;

        if (updateEncryptedCredentials)
        {
            EncryptedCredentialsJson = NormalizeOptional(encryptedCredentialsJson);
        }

        if (updateExternalSecretKey)
        {
            ExternalSecretKey = NormalizeOptional(externalSecretKey);
        }

        LastRotatedAt = utcNow;
        if (secretExpiresAt.HasValue)
        {
            SecretExpiresAt = secretExpiresAt.Value;
        }

        Version++;
        UpdatedAt = utcNow;
    }

    public void RecordTestResult(bool success, long latencyMs, string? sanitizedError, DateTime? now = null)
    {
        var utcNow = now ?? DateTime.UtcNow;
        LastTestedAt = utcNow;
        if (success)
        {
            HealthStatus = latencyMs >= 3000 ? "Degraded" : "Healthy";
            LastErrorSanitized = null;
        }
        else
        {
            HealthStatus = "Unreachable";
            LastErrorSanitized = TruncateError(sanitizedError);
        }
    }

    public void RecordUsage(bool success, long durationMs, string? sanitizedError, DateTime? now = null)
    {
        var utcNow = now ?? DateTime.UtcNow;
        LastUsedAt = utcNow;
        if (success)
        {
            HealthStatus = durationMs >= 5000 ? "Degraded" : "Healthy";
            LastErrorSanitized = null;
        }
        else
        {
            HealthStatus = "Unreachable";
            LastErrorSanitized = TruncateError(sanitizedError);
        }
    }

    private static string? TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var trimmed = error.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }

    public void Update(
        string name,
        string baseUrl,
        string authType,
        string? secretRef,
        string allowedHostsJson,
        string? defaultHeadersJson,
        string? serviceType = null,
        string? authMode = null,
        string? secretSource = null,
        string? encryptedCredentialsJson = null,
        bool updateEncryptedCredentials = false,
        string? externalSecretKey = null,
        bool updateExternalSecretKey = false,
        bool? allowWrite = null,
        int? maxRows = null)
    {
        Name = ValidateName(name);
        if (!string.IsNullOrWhiteSpace(serviceType))
        {
            ServiceType = NormalizeAndValidateServiceType(serviceType);
        }

        BaseUrl = ValidateBaseUrl(baseUrl, ServiceType);
        AuthType = NormalizeAndValidateAuthType(authType);

        if (!string.IsNullOrWhiteSpace(authMode))
        {
            AuthMode = NormalizeAndValidateAuthMode(authMode);
        }

        if (!string.IsNullOrWhiteSpace(secretSource))
        {
            SecretSource = NormalizeAndValidateSecretSource(secretSource);
        }

        SecretRef = NormalizeOptional(secretRef);

        if (updateEncryptedCredentials)
        {
            EncryptedCredentialsJson = NormalizeOptional(encryptedCredentialsJson);
        }

        if (updateExternalSecretKey)
        {
            ExternalSecretKey = NormalizeOptional(externalSecretKey);
        }

        if (allowWrite.HasValue)
        {
            AllowWrite = allowWrite.Value;
        }

        if (maxRows.HasValue)
        {
            MaxRows = ValidateMaxRows(maxRows.Value);
        }

        AllowedHostsJson = NormalizeAllowedHostsJson(allowedHostsJson);
        DefaultHeadersJson = NormalizeDefaultHeadersJson(defaultHeadersJson);
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<string> GetAllowedHosts()
    {
        if (string.IsNullOrWhiteSpace(AllowedHostsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var hosts = JsonSerializer.Deserialize<List<string>>(AllowedHostsJson);
            return hosts?
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyDictionary<string, string> GetDefaultHeaders()
    {
        if (string.IsNullOrWhiteSpace(DefaultHeadersJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(DefaultHeadersJson);
            return dict != null
                ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Connection name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static int ValidateMaxRows(int maxRows)
    {
        if (maxRows < MinMaxRows || maxRows > MaxAllowedRows)
        {
            throw new ArgumentException(
                $"MaxRows must be between {MinMaxRows} and {MaxAllowedRows}.",
                nameof(maxRows));
        }

        return maxRows;
    }

    private static string ValidateBaseUrl(string? baseUrl, string serviceType)
    {
        string trimmed = baseUrl?.Trim() ?? string.Empty;

        if (string.Equals(serviceType, "rest", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException("Connection BaseUrl is required for REST connections.", nameof(baseUrl));
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Connection BaseUrl must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));
            }
        }

        return trimmed;
    }

    private static string NormalizeAndValidateServiceType(string serviceType)
    {
        string normalized = string.IsNullOrWhiteSpace(serviceType)
            ? "rest"
            : serviceType.Trim().ToLowerInvariant();

        if (!AllowedServiceTypes.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported ServiceType '{serviceType}'. Allowed values: {string.Join(", ", AllowedServiceTypes)}.",
                nameof(serviceType));
        }

        return normalized;
    }

    private static string NormalizeAndValidateAuthType(string authType)
    {
        string normalized = string.IsNullOrWhiteSpace(authType)
            ? "none"
            : authType.Trim().ToLowerInvariant();

        if (!AllowedAuthTypes.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported AuthType '{authType}'. Allowed values: {string.Join(", ", AllowedAuthTypes)}.",
                nameof(authType));
        }

        return normalized;
    }

    private static string NormalizeAndValidateAuthMode(string authMode)
    {
        string normalized = string.IsNullOrWhiteSpace(authMode)
            ? "static"
            : authMode.Trim().ToLowerInvariant();

        if (!AllowedAuthModes.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported AuthMode '{authMode}'. Allowed values: {string.Join(", ", AllowedAuthModes)}.",
                nameof(authMode));
        }

        return normalized;
    }

    private static string NormalizeAndValidateSecretSource(string secretSource)
    {
        string normalized = string.IsNullOrWhiteSpace(secretSource)
            ? "internal_encrypted"
            : secretSource.Trim().ToLowerInvariant();

        if (!AllowedSecretSources.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported SecretSource '{secretSource}'. Allowed values: {string.Join(", ", AllowedSecretSources)}.",
                nameof(secretSource));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeAllowedHostsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        var list = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new ArgumentException("AllowedHostsJson must be a JSON string array.", nameof(json));

        var cleaned = list
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(cleaned);
    }

    private static string? NormalizeDefaultHeadersJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new ArgumentException("DefaultHeadersJson must be a JSON object of string key-value pairs.", nameof(json));

        if (dict.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(dict);
    }
}
