namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Typed security and secrets lifecycle audit events.
/// </summary>
public enum SecurityAuditEventType
{
    SecretCreated,
    SecretRotated,
    SecretDeleted,
    ConnectionTested,
    UserCredentialSaved,
    UserCredentialRevoked,
    AuthProviderChanged,
    SmartObjectGenerated,
    SmartObjectSchemaSynced
}

/// <summary>
/// Immutable security audit record capturing actor, target, and sanitized metadata (never secret values).
/// </summary>
public class SecurityAuditEvent
{
    public string Id { get; private set; } = string.Empty;
    public SecurityAuditEventType EventType { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SecurityAuditEvent() { } // EF Core

    public SecurityAuditEvent(
        SecurityAuditEventType eventType,
        string actorId,
        string targetType,
        string targetId,
        string? metadataJson = null,
        DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("ActorId is required for security audit events.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(targetType))
            throw new ArgumentException("TargetType is required for security audit events.", nameof(targetType));
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("TargetId is required for security audit events.", nameof(targetId));

        Id = $"sec-aud-{Guid.NewGuid():N}";
        EventType = eventType;
        ActorId = actorId.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId.Trim();
        MetadataJson = metadataJson;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
