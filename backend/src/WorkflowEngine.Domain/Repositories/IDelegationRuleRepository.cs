using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for managing temporary Out-of-Office delegation rules.
/// </summary>
public interface IDelegationRuleRepository
{
    Task<DelegationRule?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationRule>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DelegationRule>> GetByDelegatorAsync(string delegatorId, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationRule>> GetActiveRulesAsync(CancellationToken ct = default);
    void Add(DelegationRule rule);
    void Update(DelegationRule rule);
    void Remove(DelegationRule rule);
}
