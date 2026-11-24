using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for persisting and retrieving workflow definition aggregates.
/// </summary>
public interface IWorkflowDefinitionRepository
{
    Task<WorkflowDefinition?> GetByIdAsync(int definitionId, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByKeyAsync(string workflowKey, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByKeyWithVersionsAsync(string workflowKey, CancellationToken ct = default);
    Task<WorkflowVersion?> GetActiveVersionByKeyAsync(string workflowKey, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken ct = default);
    void Add(WorkflowDefinition definition);
    void Update(WorkflowDefinition definition);
}
