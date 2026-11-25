using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class InstanceEventRepository : IInstanceEventRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public InstanceEventRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<InstanceEvent>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.InstanceEvents
            .Where(e => e.InstanceId == instanceId)
            .OrderBy(e => e.Seq)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InstanceEvent>> GetByInstanceIdCursorAsync(string instanceId, int afterSeq, int limit, CancellationToken ct = default)
    {
        return await _dbContext.InstanceEvents
            .Where(e => e.InstanceId == instanceId && e.Seq > afterSeq)
            .OrderBy(e => e.Seq)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetNextSequenceAsync(string instanceId, CancellationToken ct = default)
    {
        int maxSeq = await _dbContext.InstanceEvents
            .Where(e => e.InstanceId == instanceId)
            .Select(e => (int?)e.Seq)
            .MaxAsync(ct) ?? 0;

        return maxSeq + 1;
    }

    public void Add(InstanceEvent instanceEvent)
    {
        _dbContext.InstanceEvents.Add(instanceEvent);
    }
}
