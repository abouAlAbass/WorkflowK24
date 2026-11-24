using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public WorkflowInstanceRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WorkflowInstance?> GetByIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowInstances
            .Include(i => i.Version)
            .FirstOrDefaultAsync(i => i.InstanceId == instanceId, ct);
    }

    public async Task<WorkflowInstance?> GetByIdWithDetailsAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowInstances
            .Include(i => i.Version)
            .Include(i => i.Tasks)
            .Include(i => i.Errors)
            .Include(i => i.AuditLogs)
            .FirstOrDefaultAsync(i => i.InstanceId == instanceId, ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetRunningInstancesAsync(string? nodeId = null, CancellationToken ct = default)
    {
        var query = _dbContext.WorkflowInstances
            .Include(i => i.Tasks.Where(t => t.Status == TaskStatus.Pending))
            .Where(i => i.Status == WorkflowStatus.Running);

        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            query = query.Where(i => i.CurrentNodeId == nodeId);
        }

        return await query.OrderBy(i => i.StartedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetAllAsync(WorkflowStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.WorkflowInstances.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query.OrderByDescending(i => i.StartedAt).ToListAsync(ct);
    }

    public void Add(WorkflowInstance instance)
    {
        _dbContext.WorkflowInstances.Add(instance);
    }

    public void Update(WorkflowInstance instance)
    {
        _dbContext.WorkflowInstances.Update(instance);
    }
}
