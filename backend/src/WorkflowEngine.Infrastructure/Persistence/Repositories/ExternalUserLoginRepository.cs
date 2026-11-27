using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ExternalUserLoginRepository : IExternalUserLoginRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ExternalUserLoginRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ExternalUserLogin?> GetByProviderAndSubjectAsync(
        string providerId,
        string externalSubject,
        CancellationToken ct = default)
    {
        return await _context.ExternalUserLogins
            .FirstOrDefaultAsync(
                e => e.ProviderId == providerId && e.ExternalSubject == externalSubject,
                ct);
    }

    public async Task<IReadOnlyList<ExternalUserLogin>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _context.ExternalUserLogins
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.LastLoginAt)
            .ToListAsync(ct);
    }

    public async Task<ExternalUserLogin> AddAsync(ExternalUserLogin login, CancellationToken ct = default)
    {
        await _context.ExternalUserLogins.AddAsync(login, ct);
        await _context.SaveChangesAsync(ct);
        return login;
    }

    public async Task<ExternalUserLogin> UpdateAsync(ExternalUserLogin login, CancellationToken ct = default)
    {
        _context.ExternalUserLogins.Update(login);
        await _context.SaveChangesAsync(ct);
        return login;
    }
}
