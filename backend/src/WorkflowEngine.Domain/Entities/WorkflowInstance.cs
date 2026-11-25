using System.Text.Json;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Root aggregate representing a runtime execution instance of a workflow.
/// Concurrency-controlled via Version token and governed by WorkflowInstanceStateMachine.
/// </summary>
public class WorkflowInstance
{
    public string InstanceId { get; private set; } = default!;
    public int VersionId { get; private set; }
    public string WorkflowKey { get; private set; } = default!;
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Running;
    public WorkflowStatus? SuspendedFromStatus { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public int Version { get; private set; } = 1;

    public string? CurrentNodeId { get; private set; }
    public string? CurrentNodeType { get; private set; }
    public string InitiatorId { get; private set; } = default!;
    public string? ContextVariablesJson { get; private set; }
    public string? SensitiveVariableNamesJson { get; private set; }
    public string? ExecutionStackJson { get; private set; }
    public string? SmartFormVersionId { get; private set; }
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public DateTime LastUpdatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowVersion WorkflowVersion { get; private set; } = default!;

    private readonly List<WorkflowTask> _tasks = new();
    public IReadOnlyCollection<WorkflowTask> Tasks => _tasks;

    private readonly List<WorkflowError> _errors = new();
    public IReadOnlyCollection<WorkflowError> Errors => _errors;

    private readonly List<WorkflowAuditLog> _auditLogs = new();
    public IReadOnlyCollection<WorkflowAuditLog> AuditLogs => _auditLogs;

    private readonly List<InstanceEvent> _events = new();
    public IReadOnlyCollection<InstanceEvent> Events => _events;

    private readonly List<NodeExecution> _nodeExecutions = new();
    public IReadOnlyCollection<NodeExecution> NodeExecutions => _nodeExecutions;

    private WorkflowInstance() { } // Required for ORM hydration

    public WorkflowInstance(
        string instanceId,
        int versionId,
        string workflowKey,
        string initiatorId,
        string? initialVariablesJson,
        WorkflowStatus initialStatus = WorkflowStatus.Running)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        VersionId = versionId;
        WorkflowKey = workflowKey ?? throw new ArgumentNullException(nameof(workflowKey));
        InitiatorId = initiatorId ?? throw new ArgumentNullException(nameof(initiatorId));
        ContextVariablesJson = initialVariablesJson;
        Status = initialStatus;
        Version = 1;

        if (initialStatus == WorkflowStatus.Running)
        {
            AddAuditLog(1, null, null, AuditEventType.NodeEnter, $"Workflow instance '{instanceId}' started by '{initiatorId}'", actorUserId: initiatorId);
        }
    }

    public void Start()
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Start);
        Status = WorkflowStatus.Running;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;
        AddAuditLog(NextSequenceNumber(), null, null, AuditEventType.NodeEnter, $"Workflow instance '{InstanceId}' started by '{InitiatorId}'", actorUserId: InitiatorId);
    }

    public void Wait()
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Wait);
        Status = WorkflowStatus.Waiting;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Continue()
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Continue);
        Status = WorkflowStatus.Running;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Suspend(string reason)
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Suspend);
        SuspendedFromStatus = Status;
        SuspendedAt = DateTime.UtcNow;
        Status = WorkflowStatus.Suspended;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;
        AddAuditLog(NextSequenceNumber(), CurrentNodeId, CurrentNodeType, AuditEventType.NodeExit, $"Execution suspended: {reason}");
    }

    public void Resume(string? reason = null)
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Resume, SuspendedFromStatus);
        Status = SuspendedFromStatus ?? WorkflowStatus.Running;
        SuspendedFromStatus = null;
        SuspendedAt = null;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;
        AddAuditLog(NextSequenceNumber(), CurrentNodeId, CurrentNodeType, AuditEventType.NodeEnter, $"Execution resumed: {reason ?? "Resumed"}");
    }

    public void Cancel(string reason)
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Cancel);
        Status = WorkflowStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;

        foreach (var task in _tasks.Where(t => t.Status == TaskStatus.Pending))
        {
            task.Cancel();
        }

        foreach (var activeNode in _nodeExecutions.Where(n => n.Status is "Running" or "Waiting"))
        {
            activeNode.MarkCancelled(CompletedAt);
        }

        AddAuditLog(NextSequenceNumber(), CurrentNodeId, CurrentNodeType, AuditEventType.NodeExit, $"Execution cancelled: {reason}");
    }

    public void Retry(string? reason = null, string? fromNodeId = null)
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Retry);
        Status = WorkflowStatus.Running;
        if (!string.IsNullOrWhiteSpace(fromNodeId))
        {
            CurrentNodeId = fromNodeId;
        }

        if (!string.IsNullOrWhiteSpace(CurrentNodeId))
        {
            var existingExec = _nodeExecutions
                .Where(n => n.NodeId == CurrentNodeId)
                .OrderByDescending(n => n.StartedAt)
                .ThenByDescending(n => n.Id)
                .FirstOrDefault();

            if (existingExec != null)
            {
                existingExec.IncrementAttempt();
            }
            else
            {
                _nodeExecutions.Add(new NodeExecution(InstanceId, CurrentNodeId, CurrentNodeType ?? "unknown", "Running", attempts: 1));
            }
        }

        LastUpdatedAt = DateTime.UtcNow;
        Version++;
        AddAuditLog(NextSequenceNumber(), CurrentNodeId, CurrentNodeType, AuditEventType.NodeEnter, $"Execution retry requested: {reason ?? "Retry"}");
    }

    public void TransitionToNode(string nodeId, string nodeType, string? outcomeBranch = null, bool isTerminalWorkflowEnd = true)
    {
        CurrentNodeId = nodeId;
        CurrentNodeType = nodeType;
        LastUpdatedAt = DateTime.UtcNow;

        int nextSeq = NextSequenceNumber();
        AddAuditLog(nextSeq, nodeId, nodeType, AuditEventType.NodeEnter, $"Transitioned to node '{nodeId}' ({nodeType})", outcomeBranch);

        if (isTerminalWorkflowEnd && nodeType.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Complete);
            Status = WorkflowStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            ExecutionStackJson = null;
            Version++;
            AddAuditLog(NextSequenceNumber(), nodeId, nodeType, AuditEventType.NodeExit, "Workflow execution completed successfully.");
        }
    }

    public List<string> GetStageStack()
    {
        if (string.IsNullOrWhiteSpace(ExecutionStackJson))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(ExecutionStackJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void PushStage(string stageNodeId)
    {
        var stack = GetStageStack();
        stack.Add(stageNodeId);
        ExecutionStackJson = JsonSerializer.Serialize(stack);
        LastUpdatedAt = DateTime.UtcNow;
    }

    public string? PopStage()
    {
        var stack = GetStageStack();
        if (stack.Count == 0)
        {
            return null;
        }

        string popped = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        ExecutionStackJson = stack.Count > 0 ? JsonSerializer.Serialize(stack) : null;
        LastUpdatedAt = DateTime.UtcNow;
        return popped;
    }

    public void SetStageStack(IEnumerable<string> stageIds)
    {
        var list = stageIds.ToList();
        ExecutionStackJson = list.Count > 0 ? JsonSerializer.Serialize(list) : null;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public WorkflowTask CreateTask(string taskId, string nodeId, string taskTitle, AssigneeType assigneeType, string assigneeId, int? dueDateDays)
    {
        DateTime? dueDate = dueDateDays.HasValue ? DateTime.UtcNow.AddDays(dueDateDays.Value) : null;
        var task = new WorkflowTask(taskId, InstanceId, nodeId, taskTitle, assigneeType, assigneeId, dueDate);
        _tasks.Add(task);

        AddAuditLog(NextSequenceNumber(), nodeId, "approval", AuditEventType.TaskAssigned, $"Approval task '{taskTitle}' assigned to {assigneeType} '{assigneeId}'");
        return task;
    }

    public void RecordFailure(string nodeId, string nodeType, string errorCode, string errorMessage, string? stackTrace, string? inputSnapshot)
    {
        WorkflowInstanceStateMachine.ValidateTransition(Status, WorkflowTrigger.Fault);
        Status = WorkflowStatus.Faulted;
        LastUpdatedAt = DateTime.UtcNow;
        Version++;

        var error = new WorkflowError(InstanceId, nodeId, nodeType, errorCode, errorMessage, stackTrace, inputSnapshot);
        _errors.Add(error);

        var nodeExec = _nodeExecutions
            .Where(n => n.NodeId == nodeId)
            .OrderByDescending(n => n.StartedAt)
            .ThenByDescending(n => n.Id)
            .FirstOrDefault();

        if (nodeExec != null)
        {
            nodeExec.MarkFailed(errorMessage);
        }
        else
        {
            _nodeExecutions.Add(new NodeExecution(InstanceId, nodeId, nodeType, "Failed", attempts: 1, startedAt: DateTime.UtcNow, endedAt: DateTime.UtcNow, errorMessage: errorMessage));
        }

        AddAuditLog(NextSequenceNumber(), nodeId, nodeType, AuditEventType.ErrorRaised, $"Execution failed at node '{nodeId}': {errorMessage}");
    }

    public void UpdateVariables(string updatedVariablesJson)
    {
        ContextVariablesJson = updatedVariablesJson;
        LastUpdatedAt = DateTime.UtcNow;
        AddAuditLog(NextSequenceNumber(), CurrentNodeId, CurrentNodeType, AuditEventType.VariableUpdated, "Runtime context variables updated.");
    }

    public void PinSmartFormVersion(string smartFormVersionId)
    {
        if (string.IsNullOrWhiteSpace(smartFormVersionId))
        {
            throw new ArgumentException("SmartFormVersionId is required.", nameof(smartFormVersionId));
        }

        SmartFormVersionId = smartFormVersionId.Trim();
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void RecordTaskDecision(
        string nodeId,
        string actorUserId,
        string outcome,
        string? updatedVariablesJson = null,
        string? beforeDiffJson = null,
        string? afterDiffJson = null)
    {
        if (updatedVariablesJson != null)
        {
            ContextVariablesJson = updatedVariablesJson;
        }

        LastUpdatedAt = DateTime.UtcNow;

        var eventType = beforeDiffJson != null ? AuditEventType.VariableUpdated : AuditEventType.TaskCompleted;
        string summary = beforeDiffJson != null
            ? $"Task at node '{nodeId}' completed by '{actorUserId}' with outcome '{outcome}' and form data updates."
            : $"Task at node '{nodeId}' completed by '{actorUserId}' with outcome '{outcome}'.";

        AddAuditLog(
            NextSequenceNumber(),
            nodeId,
            "approval",
            eventType,
            summary,
            outcome,
            actorUserId,
            beforeDiffJson,
            afterDiffJson);
    }

    public InstanceEvent AddEvent(string type, string? nodeId, string? actorId, string actorType, string? payloadJson, int? explicitSeq = null)
    {
        int seq = explicitSeq ?? (_events.Count == 0 ? 1 : _events.Max(e => e.Seq) + 1);
        var @event = new InstanceEvent(InstanceId, seq, type, nodeId, actorId, actorType, payloadJson);
        _events.Add(@event);
        return @event;
    }

    public NodeExecution RecordNodeStarted(string nodeId, string nodeType)
    {
        var existing = _nodeExecutions
            .Where(n => n.NodeId == nodeId && (n.Status == "Running" || n.Status == "Waiting"))
            .OrderByDescending(n => n.StartedAt)
            .ThenByDescending(n => n.Id)
            .FirstOrDefault();

        if (existing != null)
        {
            return existing;
        }

        var execution = new NodeExecution(InstanceId, nodeId, nodeType, "Running", attempts: 1);
        _nodeExecutions.Add(execution);
        return execution;
    }

    public void MarkNodeWaiting(string nodeId)
    {
        var execution = _nodeExecutions
            .Where(n => n.NodeId == nodeId)
            .OrderByDescending(n => n.StartedAt)
            .ThenByDescending(n => n.Id)
            .FirstOrDefault();

        execution?.MarkWaiting();
    }

    public void CompleteNodeExecution(string nodeId)
    {
        var execution = _nodeExecutions
            .Where(n => n.NodeId == nodeId && n.Status != "Completed")
            .OrderByDescending(n => n.StartedAt)
            .ThenByDescending(n => n.Id)
            .FirstOrDefault();

        execution?.MarkCompleted();
    }

    public IReadOnlyList<string> GetSensitiveVariableNames()
    {
        if (string.IsNullOrWhiteSpace(SensitiveVariableNamesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(SensitiveVariableNamesJson) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void SetSensitiveVariableNames(IEnumerable<string> variableNames)
    {
        var distinct = variableNames
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SensitiveVariableNamesJson = distinct.Count > 0 ? JsonSerializer.Serialize(distinct) : null;
        LastUpdatedAt = DateTime.UtcNow;
    }

    private int NextSequenceNumber()
    {
        return _auditLogs.Count == 0 ? 1 : _auditLogs.Max(a => a.SequenceNumber) + 1;
    }

    private void AddAuditLog(
        int seq,
        string? nodeId,
        string? nodeType,
        AuditEventType eventType,
        string summary,
        string? outcome = null,
        string? actorUserId = null,
        string? beforeJson = null,
        string? afterJson = null)
    {
        _auditLogs.Add(new WorkflowAuditLog(
            InstanceId,
            seq,
            nodeId,
            nodeType,
            eventType,
            summary,
            outcome,
            ContextVariablesJson,
            actorUserId,
            beforeJson,
            afterJson));
    }
}
