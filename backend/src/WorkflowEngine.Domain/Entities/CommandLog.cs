namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Operational command log ensuring strict command idempotency across workflow instances.
/// </summary>
public class CommandLog
{
    public Guid CommandId { get; private set; }
    public string InstanceId { get; private set; } = default!;
    public string CommandType { get; private set; } = default!;
    public string ResultJson { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowInstance? Instance { get; private set; }

    private CommandLog() { } // Required for ORM hydration

    public CommandLog(
        Guid commandId,
        string instanceId,
        string commandType,
        string resultJson,
        DateTime? createdAt = null)
    {
        CommandId = commandId;
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        ResultJson = resultJson ?? throw new ArgumentNullException(nameof(resultJson));
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
