using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for persisting and querying workflow runtime execution instances.
/// </summary>
public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(string instanceId, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdWithDetailsAsync(string instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetRunningInstancesAsync(string? nodeId = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetAllAsync(WorkflowStatus? status = null, CancellationToken ct = default);
    void Add(WorkflowInstance instance);
    void Update(WorkflowInstance instance);
}
