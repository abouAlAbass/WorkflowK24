using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IExternalUserTokenRepository
{
    Task<ExternalUserToken?> GetByUserAndProviderAsync(string userId, string providerId, CancellationToken ct = default);
    Task<ExternalUserToken?> GetLatestByUserAsync(string userId, CancellationToken ct = default);
    Task<ExternalUserToken?> GetByAnyUserIdAsync(IEnumerable<string> userIds, string? providerId = null, CancellationToken ct = default);
    Task AddAsync(ExternalUserToken token, CancellationToken ct = default);
    Task UpdateAsync(ExternalUserToken token, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
