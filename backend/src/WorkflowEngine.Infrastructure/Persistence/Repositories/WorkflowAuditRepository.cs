using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class WorkflowAuditRepository : IWorkflowAuditRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public WorkflowAuditRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<WorkflowAuditLog>> GetTimelineByInstanceIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowAuditLogs
            .Where(a => a.InstanceId == instanceId)
            .OrderBy(a => a.SequenceNumber)
            .ToListAsync(ct);
    }

    public void Add(WorkflowAuditLog log)
    {
        _dbContext.WorkflowAuditLogs.Add(log);
    }
}
