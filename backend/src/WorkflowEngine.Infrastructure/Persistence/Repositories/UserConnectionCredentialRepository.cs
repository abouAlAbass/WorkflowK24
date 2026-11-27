using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class UserConnectionCredentialRepository : IUserConnectionCredentialRepository
{
    private readonly WorkflowEngineDbContext _context;

    public UserConnectionCredentialRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<UserConnectionCredential>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        string normalized = userId.Trim().ToLowerInvariant();
        return await _context.UserConnectionCredentials
            .AsNoTracking()
            .Include(u => u.Connection)
            .Where(u => u.UserId.ToLower() == normalized)
            .OrderByDescending(u => u.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<UserConnectionCredential?> GetByUserAndConnectionAsync(string userId, string connectionId, CancellationToken ct = default)
    {
        string normalized = userId.Trim().ToLowerInvariant();
        return await _context.UserConnectionCredentials
            .FirstOrDefaultAsync(u =>
                u.UserId.ToLower() == normalized &&
                u.ConnectionId == connectionId, ct);
    }

    public async Task<UserConnectionCredential?> GetByAnyUserIdAndConnectionAsync(IEnumerable<string> userIds, string connectionId, CancellationToken ct = default)
    {
        var candidates = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return await _context.UserConnectionCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.ConnectionId == connectionId &&
                candidates.Contains(u.UserId.ToLower()), ct);
    }

    public void Add(UserConnectionCredential credential)
    {
        _context.UserConnectionCredentials.Add(credential);
    }

    public void Remove(UserConnectionCredential credential)
    {
        _context.UserConnectionCredentials.Remove(credential);
    }
}
