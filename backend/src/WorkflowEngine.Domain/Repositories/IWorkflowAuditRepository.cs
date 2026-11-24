using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for writing and reading immutable workflow execution audit logs.
/// </summary>
public interface IWorkflowAuditRepository
{
    Task<IReadOnlyList<WorkflowAuditLog>> GetTimelineByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    void Add(WorkflowAuditLog log);
}
