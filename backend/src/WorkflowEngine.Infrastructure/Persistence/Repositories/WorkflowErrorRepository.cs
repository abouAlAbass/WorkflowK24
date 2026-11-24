using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class WorkflowErrorRepository : IWorkflowErrorRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public WorkflowErrorRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WorkflowError?> GetLatestErrorByInstanceIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowInstanceErrors
            .Where(e => e.InstanceId == instanceId)
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowError>> GetErrorsByInstanceIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowInstanceErrors
            .Where(e => e.InstanceId == instanceId)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(ct);
    }

    public void Add(WorkflowError error)
    {
        _dbContext.WorkflowInstanceErrors.Add(error);
    }

    public void Update(WorkflowError error)
    {
        _dbContext.WorkflowInstanceErrors.Update(error);
    }
}
