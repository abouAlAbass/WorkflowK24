namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Concurrency-controlled synchronization barrier for a Split/Join block.
/// Uses optimistic locking (Version) and JoinFired to guarantee downstream
/// continuation triggers at most once even under concurrent branch arrivals.
/// </summary>
public class JoinState
{
    public string Id { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public string SplitNodeId { get; private set; } = default!;
    public string JoinNodeId { get; private set; } = default!;
    public int ExpectedCount { get; private set; }
    public int ArrivedCount { get; private set; }
    public bool JoinFired { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowInstance Instance { get; private set; } = default!;

    private JoinState() { } // Required for ORM hydration

    public JoinState(
        string id,
        string instanceId,
        string splitNodeId,
        string joinNodeId,
        int expectedCount)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentNullException(nameof(id)) : id.Trim();
        InstanceId = string.IsNullOrWhiteSpace(instanceId) ? throw new ArgumentNullException(nameof(instanceId)) : instanceId.Trim();
        SplitNodeId = string.IsNullOrWhiteSpace(splitNodeId) ? throw new ArgumentNullException(nameof(splitNodeId)) : splitNodeId.Trim();
        JoinNodeId = string.IsNullOrWhiteSpace(joinNodeId) ? throw new ArgumentNullException(nameof(joinNodeId)) : joinNodeId.Trim();
        ExpectedCount = Math.Max(0, expectedCount);
        ArrivedCount = 0;
        JoinFired = false;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Records the arrival of a branch token at the Join barrier and bumps concurrency Version.
    /// </summary>
    public void RecordArrival()
    {
        ArrivedCount++;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Atomically transitions JoinFired from false to true.
    /// Returns true if this call won the right to trigger downstream execution; false if already fired.
    /// </summary>
    public bool TryMarkFired()
    {
        if (JoinFired)
        {
            return false;
        }

        JoinFired = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
        return true;
    }
}
