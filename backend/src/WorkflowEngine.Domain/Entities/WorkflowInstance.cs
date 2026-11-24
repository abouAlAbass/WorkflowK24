using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Root aggregate representing a runtime execution instance of a workflow.
/// </summary>
public class WorkflowInstance
{
    public string InstanceId { get; private set; } = default!;
    public int VersionId { get; private set; }
    public string WorkflowKey { get; private set; } = default!;
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Running;
    public string? CurrentNodeId { get; private set; }
    public string? CurrentNodeType { get; private set; }
    public string InitiatorId { get; private set; } = default!;
    public string? ContextVariablesJson { get; private set; }
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public DateTime LastUpdatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowVersion Version { get; private set; } = default!;

    private readonly List<WorkflowTask> _tasks = new();
    public IReadOnlyCollection<WorkflowTask> Tasks => _tasks.AsReadOnly();

    private readonly List<WorkflowError> _errors = new();
    public IReadOnlyCollection<WorkflowError> Errors => _errors.AsReadOnly();

    private readonly List<WorkflowAuditLog> _auditLogs = new();
    public IReadOnlyCollection<WorkflowAuditLog> AuditLogs => _auditLogs.AsReadOnly();

    private WorkflowInstance() { } // Required for ORM hydration

    public WorkflowInstance(string instanceId, int versionId, string workflowKey, string initiatorId, string? initialVariablesJson)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        VersionId = versionId;
        WorkflowKey = workflowKey ?? throw new ArgumentNullException(nameof(workflowKey));
        InitiatorId = initiatorId ?? throw new ArgumentNullException(nameof(initiatorId));
        ContextVariablesJson = initialVariablesJson;
        Status = WorkflowStatus.Running;

        AddAuditLog(1, null, null, AuditEventType.NodeEnter, $"Workflow instance '{instanceId}' started by '{initiatorId}'");
    }

    public void TransitionToNode(string nodeId, string nodeType, string? outcomeBranch = null)
    {
        CurrentNodeId = nodeId;
        CurrentNodeType = nodeType;
        LastUpdatedAt = DateTime.UtcNow;

        int nextSeq = _auditLogs.Count + 1;
        AddAuditLog(nextSeq, nodeId, nodeType, AuditEventType.NodeEnter, $"Transitioned to node '{nodeId}' ({nodeType})", outcomeBranch);

        if (nodeType.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            Status = WorkflowStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            AddAuditLog(nextSeq + 1, nodeId, nodeType, AuditEventType.NodeExit, "Workflow execution completed successfully.");
        }
    }

    public WorkflowTask CreateTask(string taskId, string nodeId, string taskTitle, AssigneeType assigneeType, string assigneeId, int? dueDateDays)
    {
        DateTime? dueDate = dueDateDays.HasValue ? DateTime.UtcNow.AddDays(dueDateDays.Value) : null;
        var task = new WorkflowTask(taskId, InstanceId, nodeId, taskTitle, assigneeType, assigneeId, dueDate);
        _tasks.Add(task);

        AddAuditLog(_auditLogs.Count + 1, nodeId, "approval", AuditEventType.TaskAssigned, $"Approval task '{taskTitle}' assigned to {assigneeType} '{assigneeId}'");
        return task;
    }

    public void RecordFailure(string nodeId, string nodeType, string errorCode, string errorMessage, string? stackTrace, string? inputSnapshot)
    {
        Status = WorkflowStatus.Failed;
        LastUpdatedAt = DateTime.UtcNow;

        var error = new WorkflowError(InstanceId, nodeId, nodeType, errorCode, errorMessage, stackTrace, inputSnapshot);
        _errors.Add(error);

        AddAuditLog(_auditLogs.Count + 1, nodeId, nodeType, AuditEventType.ErrorRaised, $"Execution failed at node '{nodeId}': {errorMessage}");
    }

    public void UpdateVariables(string updatedVariablesJson)
    {
        ContextVariablesJson = updatedVariablesJson;
        LastUpdatedAt = DateTime.UtcNow;
        AddAuditLog(_auditLogs.Count + 1, CurrentNodeId, CurrentNodeType, AuditEventType.VariableUpdated, "Runtime context variables updated.");
    }

    public void Suspend(string reason)
    {
        Status = WorkflowStatus.Suspended;
        LastUpdatedAt = DateTime.UtcNow;
        AddAuditLog(_auditLogs.Count + 1, CurrentNodeId, CurrentNodeType, AuditEventType.NodeExit, $"Execution suspended: {reason}");
    }

    public void Cancel(string reason)
    {
        Status = WorkflowStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
        AddAuditLog(_auditLogs.Count + 1, CurrentNodeId, CurrentNodeType, AuditEventType.NodeExit, $"Execution cancelled: {reason}");
    }

    private void AddAuditLog(int seq, string? nodeId, string? nodeType, AuditEventType eventType, string summary, string? outcome = null)
    {
        _auditLogs.Add(new WorkflowAuditLog(InstanceId, seq, nodeId, nodeType, eventType, summary, outcome, ContextVariablesJson));
    }
}
