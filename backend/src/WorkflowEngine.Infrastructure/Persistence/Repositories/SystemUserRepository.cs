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

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var user = await _context.SystemUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return false;

        _context.SystemUsers.Remove(user);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
