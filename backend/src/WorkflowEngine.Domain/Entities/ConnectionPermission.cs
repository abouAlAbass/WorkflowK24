namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Fine-grained governance rule controlling which users or roles can use or manage a Connection.
/// </summary>
public class ConnectionPermission
{
    public static readonly IReadOnlySet<string> AllowedSubjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "User",
        "Role"
    };

    public string Id { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string SubjectType { get; private set; } = "User";
    public string SubjectId { get; private set; } = default!;
    public bool CanUse { get; private set; }
    public bool CanManage { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public Connection Connection { get; private set; } = default!;

    private ConnectionPermission() { } // EF Core

    public ConnectionPermission(
        string connectionId,
        string subjectType,
        string subjectId,
        bool canUse,
        bool canManage,
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("ConnectionId is required.", nameof(connectionId));
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));

        Id = string.IsNullOrWhiteSpace(id) ? $"cperm-{Guid.NewGuid():N}"[..24] : id.Trim();
        ConnectionId = connectionId.Trim();
        SubjectType = NormalizeSubjectType(subjectType);
        SubjectId = subjectId.Trim();
        CanManage = canManage;
        CanUse = canUse || canManage; // CanManage implies CanUse
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePermissions(bool canUse, bool canManage)
    {
        CanManage = canManage;
        CanUse = canUse || canManage;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeSubjectType(string subjectType)
    {
        if (string.Equals(subjectType?.Trim(), "Role", StringComparison.OrdinalIgnoreCase))
            return "Role";
        if (string.Equals(subjectType?.Trim(), "User", StringComparison.OrdinalIgnoreCase))
            return "User";

        throw new ArgumentException(
            $"Unsupported SubjectType '{subjectType}'. Allowed values: User, Role.",
            nameof(subjectType));
    }
}
