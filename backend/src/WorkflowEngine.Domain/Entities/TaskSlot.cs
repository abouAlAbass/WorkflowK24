using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Tracks an individual user's claim or decision slot within a group/role workflow task
/// (Claim, Consensus, or Quorum resolution policies).
/// </summary>
public class TaskSlot
{
    public long Id { get; private set; }
    public string TaskId { get; private set; } = default!;
    public string AssigneeUserId { get; private set; } = default!;
    public TaskSlotStatus Status { get; private set; } = TaskSlotStatus.Pending;
    public string? Outcome { get; private set; }
    public string? Comments { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CompletedByUserId { get; private set; }
    public string? OnBehalfOfUserId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowTask Task { get; private set; } = default!;

    private TaskSlot() { } // EF Core

    public TaskSlot(string taskId, string assigneeUserId, TaskSlotStatus status = TaskSlotStatus.Pending)
    {
        TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        AssigneeUserId = !string.IsNullOrWhiteSpace(assigneeUserId)
            ? assigneeUserId.Trim()
            : throw new ArgumentException("AssigneeUserId is required.", nameof(assigneeUserId));
        Status = status;
        CreatedAt = DateTime.UtcNow;
        if (status == TaskSlotStatus.Claimed)
        {
            ClaimedAt = DateTime.UtcNow;
        }
    }

    public void Claim()
    {
        Status = TaskSlotStatus.Claimed;
        ClaimedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        Status = TaskSlotStatus.Released;
        ClaimedAt = null;
    }

    public void Complete(string outcome, string completedByUserId, string? onBehalfOfUserId = null, string? comments = null)
    {
        Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        CompletedByUserId = completedByUserId ?? throw new ArgumentNullException(nameof(completedByUserId));
        OnBehalfOfUserId = onBehalfOfUserId;
        Comments = comments;
        Status = TaskSlotStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}
