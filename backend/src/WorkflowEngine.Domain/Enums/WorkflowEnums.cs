namespace WorkflowEngine.Domain.Enums;

/// <summary>
/// Execution status of a workflow instance.
/// </summary>
public enum WorkflowStatus
{
    Running = 1,
    Completed = 2,
    Suspended = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// Status of an assigned workflow approval or task.
/// </summary>
public enum TaskStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Delegated = 4,
    Expired = 5
}

/// <summary>
/// Target participant type for a workflow task.
/// </summary>
public enum AssigneeType
{
    User = 1,
    Group = 2
}

/// <summary>
/// Classification of logged chronological workflow execution events.
/// </summary>
public enum AuditEventType
{
    NodeEnter = 1,
    NodeExit = 2,
    DecisionEvaluated = 3,
    TaskAssigned = 4,
    VariableUpdated = 5,
    ErrorRaised = 6
}
