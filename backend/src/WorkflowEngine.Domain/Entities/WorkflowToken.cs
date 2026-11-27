using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents an individual execution token inside a workflow instance,
/// enabling parallel branch execution, branch-local variable isolation,
/// and deterministic resynchronization at Join nodes.
/// </summary>
public class WorkflowToken
{
    public string Id { get; private set; } = default!;
    public string InstanceId { get; private set; } = default!;
    public string? ParentTokenId { get; private set; }
    public string NodeId { get; private set; } = default!;
    public TokenStatus Status { get; private set; } = TokenStatus.Active;
    public string? BranchId { get; private set; }
    public string? SplitNodeId { get; private set; }
    public string? BranchVariablesJson { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }

    public WorkflowInstance Instance { get; private set; } = default!;

    public bool IsOpen => Status is TokenStatus.Active or TokenStatus.Waiting;

    private WorkflowToken() { } // Required for ORM hydration

    public WorkflowToken(
        string id,
        string instanceId,
        string nodeId,
        string? parentTokenId = null,
        string? branchId = null,
        string? splitNodeId = null,
        string? branchVariablesJson = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentNullException(nameof(id)) : id.Trim();
        InstanceId = string.IsNullOrWhiteSpace(instanceId) ? throw new ArgumentNullException(nameof(instanceId)) : instanceId.Trim();
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? throw new ArgumentNullException(nameof(nodeId)) : nodeId.Trim();
        ParentTokenId = string.IsNullOrWhiteSpace(parentTokenId) ? null : parentTokenId.Trim();
        BranchId = string.IsNullOrWhiteSpace(branchId) ? null : branchId.Trim();
        SplitNodeId = string.IsNullOrWhiteSpace(splitNodeId) ? null : splitNodeId.Trim();
        BranchVariablesJson = branchVariablesJson;
        Status = TokenStatus.Active;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void MoveToNode(string nodeId)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? throw new ArgumentNullException(nameof(nodeId)) : nodeId.Trim();
        Status = TokenStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void MarkWaiting(string nodeId)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? NodeId : nodeId.Trim();
        Status = TokenStatus.Waiting;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void MarkCompleted(string? nodeId = null)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            NodeId = nodeId.Trim();
        }
        Status = TokenStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt.Value;
        Version++;
    }

    public void MarkCancelled()
    {
        if (Status == TokenStatus.Cancelled) return;
        Status = TokenStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt.Value;
        Version++;
    }

    public void MarkFaulted(string? nodeId = null)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            NodeId = nodeId.Trim();
        }
        Status = TokenStatus.Faulted;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt.Value;
        Version++;
    }

    public void UpdateBranchVariables(string? branchVariablesJson)
    {
        BranchVariablesJson = branchVariablesJson;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
