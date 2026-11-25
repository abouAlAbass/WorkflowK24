namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Append-only domain event journal entry for a workflow instance.
/// Never modified or deleted.
/// </summary>
public class InstanceEvent
{
    public long Id { get; private set; }
    public string InstanceId { get; private set; } = default!;
    public int Seq { get; private set; }
    public string Type { get; private set; } = default!;
    public string? NodeId { get; private set; }
    public string? ActorId { get; private set; }
    public string ActorType { get; private set; } = "system"; // "user" | "system"
    public string? PayloadJson { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowInstance Instance { get; private set; } = default!;

    private InstanceEvent() { } // Required for ORM hydration

    public InstanceEvent(
        string instanceId,
        int seq,
        string type,
        string? nodeId,
        string? actorId,
        string actorType,
        string? payloadJson,
        DateTime? createdAt = null)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        Seq = seq;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        NodeId = nodeId;
        ActorId = actorId;
        ActorType = actorType ?? "system";
        PayloadJson = payloadJson;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
