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
    Group = 2,
    Role = 3
}

/// <summary>
/// Resolution policy governing how a workflow task (especially group/role tasks) is completed.
/// </summary>
public enum TaskResolutionPolicy
{
    Single = 1,
    Claim = 2,
    Consensus = 3,
    Quorum = 4
}

/// <summary>
/// Lifecycle status of an individual approval slot within a group/role task.
/// </summary>
public enum TaskSlotStatus
{
    Pending = 1,
    Claimed = 2,
    Completed = 3,
    Released = 4
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

/// <summary>
/// Lifecycle status of an immutable SmartFormVersion snapshot.
/// Draft versions can be edited; Published and Archived versions are strictly immutable.
/// </summary>
public enum SmartFormVersionStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>
/// Lifecycle status of a versioned SmartObject contract (ObjectKey@Version).
/// Draft versions can be modified; Published and Archived versions are strictly immutable.
/// </summary>
public enum SmartObjectStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>
/// Execution lifecycle status of a parallel workflow execution token.
/// </summary>
public enum TokenStatus
{
    Active = 1,
    Waiting = 2,
    Completed = 3,
    Cancelled = 4,
    Faulted = 5
}
