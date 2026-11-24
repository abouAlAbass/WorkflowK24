using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for retrieving workflow execution errors and diagnostics.
/// </summary>
public interface IWorkflowErrorRepository
{
    Task<WorkflowError?> GetLatestErrorByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowError>> GetErrorsByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    void Add(WorkflowError error);
    void Update(WorkflowError error);
}
