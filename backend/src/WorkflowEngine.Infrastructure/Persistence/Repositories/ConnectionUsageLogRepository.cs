using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ConnectionUsageLogRepository : IConnectionUsageLogRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ConnectionUsageLogRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ConnectionUsageLog>> GetByConnectionIdAsync(
        string connectionId,
        int limit = 50,
        CancellationToken ct = default)
    {
        int bounded = Math.Clamp(limit, 1, 200);
        return await _context.ConnectionUsageLogs
            .AsNoTracking()
            .Where(l => l.ConnectionId == connectionId)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(bounded)
            .ToListAsync(ct);
    }

    public void Add(ConnectionUsageLog log)
    {
        _context.ConnectionUsageLogs.Add(log);
    }
}
