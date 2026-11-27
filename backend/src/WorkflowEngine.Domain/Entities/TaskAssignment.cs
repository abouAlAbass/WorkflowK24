using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Append-only audit trail entry recording every assignment change on a workflow task:
/// initial | delegation | reassignment | escalation | auto_delegation | claim | release | recall.
/// </summary>
public class TaskAssignment
{
    public long Id { get; private set; }
    public string TaskId { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? FromAssigneeId { get; private set; }
    public string ToAssigneeId { get; private set; } = default!;
    public AssigneeType ToAssigneeType { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public string? Reason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowTask Task { get; private set; } = default!;

    private TaskAssignment() { } // EF Core

    public TaskAssignment(
        string taskId,
        string action,
        string? fromAssigneeId,
        string toAssigneeId,
        AssigneeType toAssigneeType,
        string actorUserId,
        string? reason = null,
        DateTime? createdAt = null)
    {
        TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        Action = !string.IsNullOrWhiteSpace(action)
            ? action.Trim().ToLowerInvariant()
            : throw new ArgumentException("Action is required.", nameof(action));
        FromAssigneeId = fromAssigneeId;
        ToAssigneeId = toAssigneeId ?? throw new ArgumentNullException(nameof(toAssigneeId));
        ToAssigneeType = toAssigneeType;
        ActorUserId = actorUserId ?? throw new ArgumentNullException(nameof(actorUserId));
        Reason = reason;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
