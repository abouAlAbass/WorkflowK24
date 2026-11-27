using System.Text.Json;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services.Security;

/// <summary>
/// Validates and evaluates OIDC RoleMappingJson (IdP group/role -> Tamam role)
/// while enforcing explicit opt-in for Admin role mappings.
/// </summary>
public static class OidcRoleMapper
{
    public const string AdminMappingConfirmationRequiredCode = "ADMIN_MAPPING_CONFIRMATION_REQUIRED";
    public const string InvalidRoleMappingJsonCode = "INVALID_ROLE_MAPPING_JSON";

    public static bool TryValidateRoleMapping(
        string? roleMappingJson,
        bool allowAdminRoleMapping,
        out Dictionary<string, string> mappings,
        out string? errorCode,
        out string? errorMessage)
    {
        mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        errorCode = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(roleMappingJson))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(roleMappingJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errorCode = InvalidRoleMappingJsonCode;
                errorMessage = "RoleMappingJson must be a JSON object mapping external group names to Tamam roles.";
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    errorCode = InvalidRoleMappingJsonCode;
                    errorMessage = $"Role mapping value for '{prop.Name}' must be a string.";
                    return false;
                }

                var targetRole = prop.Value.GetString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetRole))
                {
                    continue;
                }

                if (AuthProvider.IsAdminRole(targetRole) && !allowAdminRoleMapping)
                {
                    errorCode = AdminMappingConfirmationRequiredCode;
                    errorMessage =
                        $"RoleMappingJson maps '{prop.Name}' to Admin role '{targetRole}', which requires allowAdminRoleMapping = true.";
                    return false;
                }

                mappings[prop.Name.Trim()] = targetRole;
            }

            return true;
        }
        catch (JsonException ex)
        {
            errorCode = InvalidRoleMappingJsonCode;
            errorMessage = $"Invalid RoleMappingJson: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves the highest-privilege permitted role matching any of the user's external claims.
    /// Never returns an Admin role if <paramref name="allowAdminRoleMapping"/> is false.
    /// Returns null if no claim matched any mapping entry.
    /// </summary>
    public static string? ResolveMappedRole(
        string? roleMappingJson,
        IEnumerable<string> externalGroupsOrRoles,
        bool allowAdminRoleMapping)
    {
        if (string.IsNullOrWhiteSpace(roleMappingJson))
        {
            return null;
        }

        Dictionary<string, string> mappings;
        try
        {
            using var doc = JsonDocument.Parse(roleMappingJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var targetRole = prop.Value.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(targetRole))
                    {
                        mappings[prop.Name.Trim()] = targetRole;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        string? matched = null;
        foreach (var group in externalGroupsOrRoles)
        {
            if (string.IsNullOrWhiteSpace(group)) continue;
            if (!mappings.TryGetValue(group.Trim(), out var candidateRole)) continue;

            if (AuthProvider.IsAdminRole(candidateRole))
            {
                if (!allowAdminRoleMapping)
                {
                    continue;
                }

                // Admin is highest privilege when explicitly permitted
                return candidateRole;
            }

            matched ??= candidateRole;
        }

        return matched;
    }

    public static string ResolveEffectiveRole(
        string? roleMappingJson,
        IEnumerable<string> externalGroupsOrRoles,
        string defaultRole,
        bool allowAdminRoleMapping)
    {
        var mapped = ResolveMappedRole(roleMappingJson, externalGroupsOrRoles, allowAdminRoleMapping);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        return AuthProvider.IsAdminRole(defaultRole)
            ? AuthProvider.DefaultUserRole
            : (string.IsNullOrWhiteSpace(defaultRole) ? AuthProvider.DefaultUserRole : defaultRole.Trim());
    }
}
