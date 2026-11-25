using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class NodeExecutionRepository : INodeExecutionRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public NodeExecutionRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<NodeExecution>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.NodeExecutions
            .Where(n => n.InstanceId == instanceId)
            .OrderBy(n => n.StartedAt)
            .ThenBy(n => n.Id)
            .ToListAsync(ct);
    }

    public async Task<NodeExecution?> GetLatestByNodeAsync(string instanceId, string nodeId, CancellationToken ct = default)
    {
        return await _dbContext.NodeExecutions
            .Where(n => n.InstanceId == instanceId && n.NodeId == nodeId)
            .OrderByDescending(n => n.StartedAt)
            .ThenByDescending(n => n.Id)
            .FirstOrDefaultAsync(ct);
    }

    public void Add(NodeExecution nodeExecution)
    {
        _dbContext.NodeExecutions.Add(nodeExecution);
    }
}
