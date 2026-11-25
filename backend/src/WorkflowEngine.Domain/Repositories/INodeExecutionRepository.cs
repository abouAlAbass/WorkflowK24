using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface INodeExecutionRepository
{
    Task<IReadOnlyList<NodeExecution>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    Task<NodeExecution?> GetLatestByNodeAsync(string instanceId, string nodeId, CancellationToken ct = default);
    void Add(NodeExecution nodeExecution);
}
