using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Workflow task assigned to a user or group awaiting approval/outcome decision.
/// </summary>
public class WorkflowTask
{
    public string TaskId { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public string NodeId { get; private set; } = default!;
    public string TaskTitle { get; private set; } = default!;
    public AssigneeType AssigneeType { get; private set; }
    public string AssigneeId { get; private set; } = default!;
    public TaskStatus Status { get; private set; } = TaskStatus.Pending;
    public string? SelectedOutcome { get; private set; }
    public string? Comments { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every state change.</summary>
    public int Version { get; private set; } = 1;

    public WorkflowInstance Instance { get; private set; } = default!;

    private WorkflowTask() { } // Required for ORM hydration

    public WorkflowTask(string taskId, string instanceId, string nodeId, string taskTitle, AssigneeType assigneeType, string assigneeId, DateTime? dueDate)
    {
        TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        TaskTitle = taskTitle ?? throw new ArgumentNullException(nameof(taskTitle));
        AssigneeType = assigneeType;
        AssigneeId = assigneeId ?? throw new ArgumentNullException(nameof(assigneeId));
        DueDate = dueDate;
        Status = TaskStatus.Pending;
    }

    public void Complete(string outcome, string completedBy, string? comments = null)
    {
        SelectedOutcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        CompletedBy = completedBy ?? throw new ArgumentNullException(nameof(completedBy));
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
        AssigneeId = newAssigneeId ?? throw new ArgumentNullException(nameof(newAssigneeId));
        AssigneeType = newAssigneeType;
        CompletedBy = delegatedBy;
        Comments = comments;
        Status = TaskStatus.Delegated;
        Version++;
    }
}
