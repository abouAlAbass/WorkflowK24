using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class DelegationRuleRepository : IDelegationRuleRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public DelegationRuleRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DelegationRule?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _dbContext.DelegationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<DelegationRule>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.DelegationRules
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DelegationRule>> GetByDelegatorAsync(string delegatorId, CancellationToken ct = default)
    {
        string normalized = (delegatorId ?? string.Empty).Trim().ToLower();
        return await _dbContext.DelegationRules
            .Where(r => r.DelegatorId.ToLower() == normalized)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DelegationRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        return await _dbContext.DelegationRules
            .Where(r => r.Active)
            .OrderBy(r => r.ValidFrom)
            .ToListAsync(ct);
    }

    public void Add(DelegationRule rule)
    {
        _dbContext.DelegationRules.Add(rule);
    }

    public void Update(DelegationRule rule)
    {
        _dbContext.DelegationRules.Update(rule);
    }

    public void Remove(DelegationRule rule)
    {
        _dbContext.DelegationRules.Remove(rule);
    }
}
