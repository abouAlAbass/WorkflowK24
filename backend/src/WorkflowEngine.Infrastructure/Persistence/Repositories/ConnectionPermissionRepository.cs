using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ConnectionPermissionRepository : IConnectionPermissionRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ConnectionPermissionRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ConnectionPermission>> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default)
    {
        return await _context.ConnectionPermissions
            .AsNoTracking()
            .Where(p => p.ConnectionId == connectionId)
            .OrderBy(p => p.SubjectType)
            .ThenBy(p => p.SubjectId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConnectionPermission>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ConnectionPermissions
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<ConnectionPermission?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.ConnectionPermissions
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<ConnectionPermission?> GetBySubjectAsync(string connectionId, string subjectType, string subjectId, CancellationToken ct = default)
    {
        string normalizedType = string.Equals(subjectType?.Trim(), "Role", StringComparison.OrdinalIgnoreCase) ? "Role" : "User";
        string normalizedId = subjectId.Trim();

        return await _context.ConnectionPermissions
            .FirstOrDefaultAsync(p =>
                p.ConnectionId == connectionId &&
                p.SubjectType == normalizedType &&
                p.SubjectId.ToLower() == normalizedId.ToLower(), ct);
    }

    public void Add(ConnectionPermission permission)
    {
        _context.ConnectionPermissions.Add(permission);
    }

    public void Remove(ConnectionPermission permission)
    {
        _context.ConnectionPermissions.Remove(permission);
    }
}
