namespace WorkflowEngine.Domain.Entities;

/// <summary>Immutable audit record of an administrative action on a user account.</summary>
public class UserAuditLog
{
    public string Id { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public string ActorEmail { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetUserId { get; private set; } = string.Empty;
    public string TargetEmail { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private UserAuditLog() { } // EF Core

    public UserAuditLog(
        string actorId, string actorEmail, string action,
        string targetUserId, string targetEmail, string? details = null)
    {
        Id = $"aud-{Guid.NewGuid():N}";
        ActorId = actorId ?? string.Empty;
        ActorEmail = actorEmail ?? string.Empty;
        Action = action;
        TargetUserId = targetUserId ?? string.Empty;
        TargetEmail = targetEmail ?? string.Empty;
        Details = details;
        OccurredAt = DateTime.UtcNow;
    }
}

public static class UserAuditActions
{
    public const string Created = "UserCreated";
    public const string Updated = "UserUpdated";
    public const string Deleted = "UserDeleted";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
    public const string AccountLocked = "AccountLocked";
}
