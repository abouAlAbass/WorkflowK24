namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Database-driven external OpenID Connect identity provider configuration.
/// Client secrets are stored exclusively as Data Protection encrypted envelopes under SecretPurposes.AuthProviders.
/// </summary>
public class AuthProvider
{
    public const string DefaultProtocol = "oidc";
    public const string DefaultScopes = "openid profile email";
    public const string DefaultUserRole = "User";

    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Protocol { get; private set; } = DefaultProtocol;
    public string Authority { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string? EncryptedClientSecret { get; private set; }
    public string Scopes { get; private set; } = DefaultScopes;
    public string RoleMappingJson { get; private set; } = "{}";
    public bool JitProvisioningEnabled { get; private set; } = true;
    public bool AllowEmailMatching { get; private set; } = false;
    public bool AllowAdminRoleMapping { get; private set; } = false;
    public string DefaultRole { get; private set; } = DefaultUserRole;
    public bool IsEnabled { get; private set; } = true;
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AuthProvider() { } // EF Core

    public AuthProvider(
        string? id,
        string name,
        string protocol,
        string authority,
        string clientId,
        string? encryptedClientSecret = null,
        string? scopes = null,
        string? roleMappingJson = null,
        bool jitProvisioningEnabled = true,
        bool allowEmailMatching = false,
        bool allowAdminRoleMapping = false,
        string? defaultRole = null,
        bool isEnabled = true)
    {
        Id = !string.IsNullOrWhiteSpace(id) ? id.Trim() : $"authp-{Guid.NewGuid():N}";
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Provider name is required.", nameof(name));
        Protocol = string.IsNullOrWhiteSpace(protocol)
            ? DefaultProtocol
            : protocol.Trim().ToLowerInvariant();
        if (!string.Equals(Protocol, DefaultProtocol, StringComparison.Ordinal))
            throw new ArgumentException($"Unsupported auth protocol '{protocol}'. Only 'oidc' is supported.", nameof(protocol));

        Authority = !string.IsNullOrWhiteSpace(authority)
            ? authority.Trim().TrimEnd('/')
            : throw new ArgumentException("Authority URL is required.", nameof(authority));
        ClientId = !string.IsNullOrWhiteSpace(clientId)
            ? clientId.Trim()
            : throw new ArgumentException("ClientId is required.", nameof(clientId));
        EncryptedClientSecret = encryptedClientSecret;
        Scopes = !string.IsNullOrWhiteSpace(scopes) ? scopes.Trim() : DefaultScopes;
        RoleMappingJson = !string.IsNullOrWhiteSpace(roleMappingJson) ? roleMappingJson.Trim() : "{}";
        JitProvisioningEnabled = jitProvisioningEnabled;
        AllowEmailMatching = allowEmailMatching;
        AllowAdminRoleMapping = allowAdminRoleMapping;
        DefaultRole = ValidateDefaultRole(defaultRole);
        IsEnabled = isEnabled;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static bool IsAdminRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var trimmed = role.Trim();
        return string.Equals(trimmed, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Administrator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "BPM Administrator", StringComparison.OrdinalIgnoreCase);
    }

    public static string ValidateDefaultRole(string? role)
    {
        var resolved = string.IsNullOrWhiteSpace(role) ? DefaultUserRole : role.Trim();
        if (IsAdminRole(resolved))
        {
            throw new ArgumentException("DefaultRole cannot be an Admin role.", nameof(role));
        }

        return resolved;
    }

    public void Update(
        string name,
        string authority,
        string clientId,
        string? encryptedClientSecret,
        bool updateSecret,
        string? scopes,
        string? roleMappingJson,
        bool jitProvisioningEnabled,
        bool allowEmailMatching,
        bool allowAdminRoleMapping,
        string? defaultRole,
        bool isEnabled)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Provider name is required.", nameof(name));
        Authority = !string.IsNullOrWhiteSpace(authority)
            ? authority.Trim().TrimEnd('/')
            : throw new ArgumentException("Authority URL is required.", nameof(authority));
        ClientId = !string.IsNullOrWhiteSpace(clientId)
            ? clientId.Trim()
            : throw new ArgumentException("ClientId is required.", nameof(clientId));

        if (updateSecret)
        {
            EncryptedClientSecret = encryptedClientSecret;
        }

        Scopes = !string.IsNullOrWhiteSpace(scopes) ? scopes.Trim() : DefaultScopes;
        RoleMappingJson = !string.IsNullOrWhiteSpace(roleMappingJson) ? roleMappingJson.Trim() : "{}";
        JitProvisioningEnabled = jitProvisioningEnabled;
        AllowEmailMatching = allowEmailMatching;
        AllowAdminRoleMapping = allowAdminRoleMapping;
        DefaultRole = ValidateDefaultRole(defaultRole);
        IsEnabled = isEnabled;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
