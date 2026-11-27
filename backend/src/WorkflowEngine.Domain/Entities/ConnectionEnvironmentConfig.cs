namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Environment-specific override (Dev / Test / Prod) for a Connection's target BaseUrl and secret configuration,
/// allowing workflows to move across environments without modifying workflow definitions.
/// </summary>
public class ConnectionEnvironmentConfig
{
    public static readonly IReadOnlyDictionary<string, string> CanonicalEnvironmentNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dev"] = "Dev",
            ["Test"] = "Test",
            ["Prod"] = "Prod"
        };

    public string Id { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string EnvironmentName { get; private set; } = "Dev";
    public string? BaseUrlOverride { get; private set; }
    public string? EncryptedCredentialsJsonOverride { get; private set; }
    public string? ExternalSecretKeyOverride { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Connection? Connection { get; private set; }

    private ConnectionEnvironmentConfig() { } // EF Core

    public ConnectionEnvironmentConfig(
        string connectionId,
        string environmentName,
        string? baseUrlOverride = null,
        string? encryptedCredentialsJsonOverride = null,
        string? externalSecretKeyOverride = null,
        DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("ConnectionId is required.", nameof(connectionId));

        Id = $"cenv-{Guid.NewGuid():N}";
        ConnectionId = connectionId.Trim();
        EnvironmentName = NormalizeAndValidateEnvironmentName(environmentName);
        BaseUrlOverride = NormalizeOptional(baseUrlOverride);
        EncryptedCredentialsJsonOverride = NormalizeOptional(encryptedCredentialsJsonOverride);
        ExternalSecretKeyOverride = NormalizeOptional(externalSecretKeyOverride);
        UpdatedAt = now ?? DateTime.UtcNow;
    }

    public void Update(
        string? baseUrlOverride,
        string? encryptedCredentialsJsonOverride,
        bool updateEncryptedCredentials,
        string? externalSecretKeyOverride,
        bool updateExternalSecretKey,
        DateTime? now = null)
    {
        BaseUrlOverride = NormalizeOptional(baseUrlOverride);

        if (updateEncryptedCredentials)
        {
            EncryptedCredentialsJsonOverride = NormalizeOptional(encryptedCredentialsJsonOverride);
        }

        if (updateExternalSecretKey)
        {
            ExternalSecretKeyOverride = NormalizeOptional(externalSecretKeyOverride);
        }

        UpdatedAt = now ?? DateTime.UtcNow;
    }

    public static string NormalizeAndValidateEnvironmentName(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName) ||
            !CanonicalEnvironmentNames.TryGetValue(environmentName.Trim(), out var canonical))
        {
            throw new ArgumentException(
                $"Unsupported EnvironmentName '{environmentName}'. Allowed values: Dev, Test, Prod.",
                nameof(environmentName));
        }

        return canonical;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
