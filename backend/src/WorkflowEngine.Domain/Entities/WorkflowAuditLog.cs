using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Chronological audit trail event recording instance state transitions.
/// </summary>
public class WorkflowAuditLog
{
    public long LogId { get; private set; }
    public string InstanceId { get; private set; } = default!;
    public int SequenceNumber { get; private set; }
    public string? NodeId { get; private set; }
    public string? NodeType { get; private set; }
    public AuditEventType EventType { get; private set; }
    public string EventSummary { get; private set; } = default!;
    public string? OutcomeBranch { get; private set; }
    public string? StateSnapshotJson { get; private set; }
    public DateTime LoggedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowInstance Instance { get; private set; } = default!;

    private WorkflowAuditLog() { } // Required for ORM hydration

    public WorkflowAuditLog(
        string instanceId, 
        int seq, 
        string? nodeId, 
        string? nodeType, 
        AuditEventType eventType, 
        string summary, 
        string? outcome = null, 
        string? stateSnapshot = null)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        SequenceNumber = seq;
        NodeId = nodeId;
        NodeType = nodeType;
        EventType = eventType;
        EventSummary = summary ?? throw new ArgumentNullException(nameof(summary));
        OutcomeBranch = outcome;
        StateSnapshotJson = stateSnapshot;
        LoggedAt = DateTime.UtcNow;
    }
}
