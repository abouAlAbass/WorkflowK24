using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IAuthProviderRepository
{
    Task<IReadOnlyList<AuthProvider>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AuthProvider>> GetEnabledAsync(CancellationToken ct = default);
    Task<AuthProvider?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<AuthProvider?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<AuthProvider> AddAsync(AuthProvider provider, CancellationToken ct = default);
    Task<AuthProvider> UpdateAsync(AuthProvider provider, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
