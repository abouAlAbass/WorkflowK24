using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ExternalUserTokenRepository : IExternalUserTokenRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ExternalUserTokenRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ExternalUserToken?> GetByUserAndProviderAsync(
        string userId,
        string providerId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(providerId))
            return null;

        string u = userId.Trim();
        string p = providerId.Trim();
        return await _context.ExternalUserTokens
            .FirstOrDefaultAsync(t => t.UserId == u && t.ProviderId == p, ct);
    }

    public async Task<ExternalUserToken?> GetLatestByUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        string u = userId.Trim();
        return await _context.ExternalUserTokens
            .Where(t => t.UserId == u)
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ExternalUserToken?> GetByAnyUserIdAsync(
        IEnumerable<string> userIds,
        string? providerId = null,
        CancellationToken ct = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return null;

        var query = _context.ExternalUserTokens.Where(t => ids.Contains(t.UserId));
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            string p = providerId.Trim();
            query = query.Where(t => t.ProviderId == p);
        }

        return await query
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ExternalUserToken token, CancellationToken ct = default)
    {
        _context.ExternalUserTokens.Add(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ExternalUserToken token, CancellationToken ct = default)
    {
        _context.ExternalUserTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var existing = await _context.ExternalUserTokens.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (existing != null)
        {
            _context.ExternalUserTokens.Remove(existing);
            await _context.SaveChangesAsync(ct);
        }
    }
}
