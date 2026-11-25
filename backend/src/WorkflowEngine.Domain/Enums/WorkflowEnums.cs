namespace WorkflowEngine.Domain.Enums;

/// <summary>
/// Execution status of a workflow instance.
/// Explicit states: Created, Running, Waiting, Suspended, Faulted, Cancelled, Completed.
/// </summary>
public enum WorkflowStatus
{
    Created = 0,
    Running = 1,
    Completed = 2,
    Suspended = 3,
    Faulted = 4,
    Cancelled = 5,
    Waiting = 6,

    /// <summary>
    /// Backward-compatibility alias for Faulted.
    /// </summary>
    Failed = 4
}

/// <summary>
/// Operational triggers and transitions for workflow instances.
/// </summary>
public enum WorkflowTrigger
{
    Start,
    Wait,
    Continue,
    Suspend,
    Resume,
    Fault,
    Retry,
    Cancel,
    Complete
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
    Expired = 5,
    Cancelled = 6
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
    ErrorRaised = 6,
    TaskCompleted = 7
}
