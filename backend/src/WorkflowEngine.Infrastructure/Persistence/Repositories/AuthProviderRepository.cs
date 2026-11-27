using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class AuthProviderRepository : IAuthProviderRepository
{
    private readonly WorkflowEngineDbContext _context;

    public AuthProviderRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<AuthProvider>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.AuthProviders
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuthProvider>> GetEnabledAsync(CancellationToken ct = default)
    {
        return await _context.AuthProviders
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<AuthProvider?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.AuthProviders
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<AuthProvider?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await _context.AuthProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name.ToLower() == normalized, ct);
    }

    public async Task<AuthProvider> AddAsync(AuthProvider provider, CancellationToken ct = default)
    {
        await _context.AuthProviders.AddAsync(provider, ct);
        await _context.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<AuthProvider> UpdateAsync(AuthProvider provider, CancellationToken ct = default)
    {
        _context.AuthProviders.Update(provider);
        await _context.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var existing = await _context.AuthProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing == null) return false;

        _context.AuthProviders.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
