using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class SystemUserRepository : ISystemUserRepository
{
    private readonly WorkflowEngineDbContext _context;

    public SystemUserRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SystemUser>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SystemUsers
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SystemUser>> SearchAsync(string? query, int limit, CancellationToken ct = default)
    {
        int safeLimit = Math.Clamp(limit, 1, 50);

        IQueryable<SystemUser> dbQuery = _context.SystemUsers
            .AsNoTracking()
            .Where(u => u.Status == "Active");

        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalized = query.Trim().ToLowerInvariant();
            dbQuery = dbQuery.Where(u =>
                u.Name.ToLower().Contains(normalized) ||
                u.Email.ToLower().Contains(normalized) ||
                u.Group.ToLower().Contains(normalized));
        }

        return await dbQuery
            .OrderBy(u => u.Name)
            .Take(safeLimit)
            .ToListAsync(ct);
    }

    public async Task<SystemUser?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.SystemUsers
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<SystemUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.SystemUsers
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public async Task<SystemUser> AddAsync(SystemUser user, CancellationToken ct = default)
    {
        await _context.SystemUsers.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<SystemUser> UpdateAsync(SystemUser user, CancellationToken ct = default)
    {
        _context.SystemUsers.Update(user);
        await _context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> HasLinkedWorkAsync(string userId, string email, CancellationToken ct = default)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();

        bool hasPendingTasks = await _context.WorkflowTasks.AnyAsync(t =>
            t.Status == Domain.Enums.TaskStatus.Pending &&
            (t.AssigneeId == userId || t.AssigneeId.ToLower() == normalizedEmail), ct);
        if (hasPendingTasks) return true;

        return await _context.WorkflowInstances.AnyAsync(i =>
            i.InitiatorId == userId || i.InitiatorId.ToLower() == normalizedEmail, ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var user = await _context.SystemUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return false;

        _context.SystemUsers.Remove(user);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
