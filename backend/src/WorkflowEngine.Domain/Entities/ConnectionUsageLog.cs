namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Operational usage log entry recorded on every IServiceBroker invocation for a Connection.
/// Strictly stores execution metadata only — NEVER secrets, headers, or credentials.
/// </summary>
public class ConnectionUsageLog
{
    public string Id { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string? InstanceId { get; private set; }
    public string? WorkflowKey { get; private set; }
    public string? NodeId { get; private set; }
    public string ActorId { get; private set; } = "system";
    public bool Success { get; private set; }
    public long DurationMs { get; private set; }
    public DateTime ExecutedAt { get; private set; }

    public Connection? Connection { get; private set; }

    private ConnectionUsageLog() { } // EF Core

    public ConnectionUsageLog(
        string connectionId,
        string? instanceId,
        string? workflowKey,
        string? nodeId,
        string? actorId,
        bool success,
        long durationMs,
        DateTime? executedAt = null)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("ConnectionId is required.", nameof(connectionId));

        Id = $"cusg-{Guid.NewGuid():N}";
        ConnectionId = connectionId.Trim();
        InstanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
        WorkflowKey = string.IsNullOrWhiteSpace(workflowKey) ? null : workflowKey.Trim();
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        ActorId = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId.Trim();
        Success = success;
        DurationMs = Math.Max(0, durationMs);
        ExecutedAt = executedAt ?? DateTime.UtcNow;
    }
}
