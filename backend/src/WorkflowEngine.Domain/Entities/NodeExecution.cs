namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Read-optimized projection tracking per-node execution state, attempts, timing, and errors
/// for O(1) ViewFlow rendering. Updated atomically alongside instance state and InstanceEvents.
/// </summary>
public class NodeExecution
{
    public long Id { get; private set; }
    public string InstanceId { get; private set; } = default!;
    public string NodeId { get; private set; } = default!;
    public string NodeType { get; private set; } = default!;
    public string Status { get; private set; } = "Running";
    public int Attempts { get; private set; } = 1;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public WorkflowInstance Instance { get; private set; } = default!;

    private NodeExecution() { } // Required for ORM hydration

    public NodeExecution(
        string instanceId,
        string nodeId,
        string nodeType,
        string status = "Running",
        int attempts = 1,
        DateTime? startedAt = null,
        DateTime? endedAt = null,
        string? errorMessage = null)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        NodeType = nodeType ?? throw new ArgumentNullException(nameof(nodeType));
        Status = string.IsNullOrWhiteSpace(status) ? "Running" : status;
        Attempts = Math.Max(1, attempts);
        StartedAt = startedAt ?? DateTime.UtcNow;
        EndedAt = endedAt;
        ErrorMessage = errorMessage;
    }

    public void MarkCompleted(DateTime? endedAt = null)
    {
        Status = "Completed";
        EndedAt = endedAt ?? DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkWaiting()
    {
        Status = "Waiting";
        EndedAt = null;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage, DateTime? endedAt = null)
    {
        Status = "Failed";
        EndedAt = endedAt ?? DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void MarkCancelled(DateTime? endedAt = null)
    {
        Status = "Cancelled";
        EndedAt = endedAt ?? DateTime.UtcNow;
    }

    public void IncrementAttempt()
    {
        Attempts++;
        Status = "Running";
        EndedAt = null;
        ErrorMessage = null;
    }
}
