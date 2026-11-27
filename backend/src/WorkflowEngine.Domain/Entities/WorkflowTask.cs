using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Workflow task assigned to a user, group, or role awaiting approval/outcome decision.
/// Supports delegation, recall, reassignment, group slots (Claim/Consensus/Quorum),
/// SLA escalation, and append-only assignment history.
/// </summary>
public class WorkflowTask
{
    public string TaskId { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public string NodeId { get; private set; } = default!;
    public string? TokenId { get; private set; }
    public string? BranchId { get; private set; }
    public string TaskTitle { get; private set; } = default!;
    public AssigneeType AssigneeType { get; private set; }
    public string AssigneeId { get; private set; } = default!;
    public string OriginalAssigneeId { get; private set; } = default!;
    public AssigneeType OriginalAssigneeType { get; private set; }
    public TaskResolutionPolicy ResolutionPolicy { get; private set; } = TaskResolutionPolicy.Single;
    public int? QuorumCount { get; private set; }
    public string? ClaimedByUserId { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public TaskStatus Status { get; private set; } = TaskStatus.Pending;
    public string? SelectedOutcome { get; private set; }
    public string? Comments { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? DueAt { get; private set; }
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public string? CompletedByUserId { get; private set; }
    public string? OnBehalfOfUserId { get; private set; }
    public DateTime? EscalatedAt { get; private set; }
    public string? MetadataJson { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every state change.</summary>
    public int Version { get; private set; } = 1;

    public WorkflowInstance Instance { get; private set; } = default!;

    private readonly List<TaskSlot> _slots = new();
    public IReadOnlyCollection<TaskSlot> Slots => _slots;

    private readonly List<TaskAssignment> _assignments = new();
    public IReadOnlyCollection<TaskAssignment> Assignments => _assignments;

    public bool IsActive => Status == TaskStatus.Pending || Status == TaskStatus.Delegated;

    private WorkflowTask() { } // Required for ORM hydration

    public WorkflowTask(
        string taskId,
        string instanceId,
        string nodeId,
        string taskTitle,
        AssigneeType assigneeType,
        string assigneeId,
        DateTime? dueDate,
        TaskResolutionPolicy resolutionPolicy = TaskResolutionPolicy.Single,
        int? quorumCount = null,
        string? tokenId = null,
        string? branchId = null,
        string? metadataJson = null)
    {
        TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        TokenId = string.IsNullOrWhiteSpace(tokenId) ? null : tokenId.Trim();
        BranchId = string.IsNullOrWhiteSpace(branchId) ? null : branchId.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
        TaskTitle = taskTitle ?? throw new ArgumentNullException(nameof(taskTitle));
        AssigneeType = assigneeType;
        AssigneeId = assigneeId ?? throw new ArgumentNullException(nameof(assigneeId));
        OriginalAssigneeId = assigneeId;
        OriginalAssigneeType = assigneeType;
        ResolutionPolicy = resolutionPolicy;
        QuorumCount = quorumCount;
        DueDate = dueDate;
        DueAt = dueDate;
        Status = TaskStatus.Pending;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "initial",
            null,
            AssigneeId,
            AssigneeType,
            "system",
            "Initial task assignment",
            AssignedAt));
    }

    public void Complete(string outcome, string completedBy, string? comments = null, string? onBehalfOfUserId = null)
    {
        SelectedOutcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        CompletedBy = completedBy ?? throw new ArgumentNullException(nameof(completedBy));
        CompletedByUserId = completedBy;

        if (!string.IsNullOrWhiteSpace(onBehalfOfUserId))
        {
            OnBehalfOfUserId = onBehalfOfUserId;
        }
        else if (string.IsNullOrWhiteSpace(OnBehalfOfUserId) &&
                 !string.IsNullOrWhiteSpace(OriginalAssigneeId) &&
                 OriginalAssigneeType == AssigneeType.User &&
                 !string.Equals(OriginalAssigneeId, completedBy, StringComparison.OrdinalIgnoreCase))
        {
            OnBehalfOfUserId = OriginalAssigneeId;
        }

        Comments = comments;
        Status = outcome.Equals("reject", StringComparison.OrdinalIgnoreCase) ? TaskStatus.Rejected : TaskStatus.Approved;
        CompletedAt = DateTime.UtcNow;
        Version++;
    }

    public void Cancel()
    {
        Status = TaskStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        Version++;
    }

    public void Delegate(string newAssigneeId, AssigneeType newAssigneeType, string delegatedBy, string? comments = null)
    {
        if (string.IsNullOrWhiteSpace(newAssigneeId))
            throw new ArgumentNullException(nameof(newAssigneeId));
        if (string.IsNullOrWhiteSpace(delegatedBy))
            throw new ArgumentNullException(nameof(delegatedBy));

        string previousAssigneeId = AssigneeId;
        AssigneeId = newAssigneeId.Trim();
        AssigneeType = newAssigneeType;
        CompletedBy = delegatedBy;
        OnBehalfOfUserId = !string.IsNullOrWhiteSpace(OriginalAssigneeId) ? OriginalAssigneeId : previousAssigneeId;
        ClaimedByUserId = null;
        ClaimedAt = null;
        Comments = comments;
        Status = TaskStatus.Delegated;
        Version++;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "delegation",
            previousAssigneeId,
            AssigneeId,
            AssigneeType,
            delegatedBy,
            comments));
    }

    public void ApplyAutoDelegation(string delegateUserId, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(delegateUserId))
            throw new ArgumentNullException(nameof(delegateUserId));

        string previousAssigneeId = AssigneeId;
        AssigneeId = delegateUserId.Trim();
        AssigneeType = AssigneeType.User;
        OnBehalfOfUserId = !string.IsNullOrWhiteSpace(OriginalAssigneeId) ? OriginalAssigneeId : previousAssigneeId;
        Status = TaskStatus.Delegated;
        Version++;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "auto_delegation",
            previousAssigneeId,
            AssigneeId,
            AssigneeType.User,
            "system",
            reason ?? "Auto-delegated via active Out-of-Office rule"));
    }

    public void Reassign(string newAssigneeId, AssigneeType newAssigneeType, string actorUserId, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(newAssigneeId))
            throw new ArgumentNullException(nameof(newAssigneeId));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentNullException(nameof(actorUserId));

        string previousAssigneeId = AssigneeId;
        AssigneeId = newAssigneeId.Trim();
        AssigneeType = newAssigneeType;
        ClaimedByUserId = null;
        ClaimedAt = null;
        OnBehalfOfUserId = !string.Equals(AssigneeId, OriginalAssigneeId, StringComparison.OrdinalIgnoreCase)
            ? OriginalAssigneeId
            : null;
        Comments = reason ?? Comments;
        Status = TaskStatus.Pending;
        Version++;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "reassignment",
            previousAssigneeId,
            AssigneeId,
            AssigneeType,
            actorUserId,
            reason));
    }

    public void Recall(string actorUserId, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentNullException(nameof(actorUserId));

        string previousAssigneeId = AssigneeId;
        AssigneeId = OriginalAssigneeId;
        AssigneeType = OriginalAssigneeType;
        OnBehalfOfUserId = null;
        ClaimedByUserId = null;
        ClaimedAt = null;
        Status = TaskStatus.Pending;
        Version++;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "recall",
            previousAssigneeId,
            AssigneeId,
            AssigneeType,
            actorUserId,
            reason ?? "Task recalled by original assignee"));
    }

    public TaskSlot Claim(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        string normalizedUser = userId.Trim();
        ClaimedByUserId = normalizedUser;
        ClaimedAt = DateTime.UtcNow;
        Version++;

        var slot = _slots.FirstOrDefault(s =>
            string.Equals(s.AssigneeUserId, normalizedUser, StringComparison.OrdinalIgnoreCase));
        if (slot == null)
        {
            slot = new TaskSlot(TaskId, normalizedUser, TaskSlotStatus.Claimed);
            _slots.Add(slot);
        }
        else
        {
            slot.Claim();
        }

        _assignments.Add(new TaskAssignment(
            TaskId,
            "claim",
            AssigneeId,
            normalizedUser,
            AssigneeType.User,
            normalizedUser,
            "Task claimed by group member"));

        return slot;
    }

    public void Release(string actorUserId, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentNullException(nameof(actorUserId));

        string? previousClaimant = ClaimedByUserId;
        ClaimedByUserId = null;
        ClaimedAt = null;
        Version++;

        if (!string.IsNullOrWhiteSpace(previousClaimant))
        {
            var slot = _slots.FirstOrDefault(s =>
                string.Equals(s.AssigneeUserId, previousClaimant, StringComparison.OrdinalIgnoreCase) &&
                s.Status == TaskSlotStatus.Claimed);
            slot?.Release();
        }

        _assignments.Add(new TaskAssignment(
            TaskId,
            "release",
            previousClaimant ?? actorUserId,
            AssigneeId,
            AssigneeType,
            actorUserId,
            reason ?? "Task claim released back to group"));
    }

    public TaskSlot RecordSlotDecision(string userId, string outcome, string? comments = null, string? onBehalfOfUserId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        string normalizedUser = userId.Trim();
        var slot = _slots.FirstOrDefault(s =>
            string.Equals(s.AssigneeUserId, normalizedUser, StringComparison.OrdinalIgnoreCase));

        if (slot == null)
        {
            slot = new TaskSlot(TaskId, normalizedUser, TaskSlotStatus.Pending);
            _slots.Add(slot);
        }

        slot.Complete(outcome, normalizedUser, onBehalfOfUserId, comments);
        Version++;
        return slot;
    }

    public void Escalate(string escalationTargetId, AssigneeType escalationTargetType, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(escalationTargetId))
            throw new ArgumentNullException(nameof(escalationTargetId));

        string previousAssigneeId = AssigneeId;
        AssigneeId = escalationTargetId.Trim();
        AssigneeType = escalationTargetType;
        ClaimedByUserId = null;
        ClaimedAt = null;
        EscalatedAt = DateTime.UtcNow;
        OnBehalfOfUserId = !string.IsNullOrWhiteSpace(OriginalAssigneeId) ? OriginalAssigneeId : previousAssigneeId;
        Version++;

        _assignments.Add(new TaskAssignment(
            TaskId,
            "escalation",
            previousAssigneeId,
            AssigneeId,
            AssigneeType,
            "system",
            reason ?? "SLA due date exceeded"));
    }

    public void SetDueAt(DateTime? dueAt)
    {
        DueAt = dueAt;
        DueDate = dueAt;
    }
}
