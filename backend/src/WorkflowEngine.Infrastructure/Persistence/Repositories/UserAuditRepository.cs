using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class UserAuditRepository : IUserAuditRepository
{
    private readonly WorkflowEngineDbContext _context;

    public UserAuditRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(UserAuditLog entry, CancellationToken ct = default)
    {
        await _context.UserAuditLogs.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserAuditLog>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        return await _context.UserAuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);
    }
}
