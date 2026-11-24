namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Node-level execution diagnostic record capturing system errors and failures.
/// </summary>
public class WorkflowError
{
    public int ErrorId { get; private set; }
    public string InstanceId { get; private set; } = default!;
    public string NodeId { get; private set; } = default!;
    public string NodeType { get; private set; } = default!;
    public string ErrorCode { get; private set; } = default!;
    public string ErrorMessage { get; private set; } = default!;
    public string? StackTrace { get; private set; }
    public string? InputSnapshotJson { get; private set; }
    public int RetryCount { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; private set; }

    public WorkflowInstance Instance { get; private set; } = default!;

    private WorkflowError() { } // Required for ORM hydration

    public WorkflowError(string instanceId, string nodeId, string nodeType, string errorCode, string errorMessage, string? stackTrace, string? inputSnapshot)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        NodeType = nodeType ?? throw new ArgumentNullException(nameof(nodeType));
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        StackTrace = stackTrace;
        InputSnapshotJson = inputSnapshot;
        OccurredAt = DateTime.UtcNow;
        RetryCount = 0;
        IsResolved = false;
    }

    public void IncrementRetry()
    {
        RetryCount++;
    }

    public void Resolve()
    {
        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
    }
}
