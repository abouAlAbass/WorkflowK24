using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IExternalUserLoginRepository
{
    Task<ExternalUserLogin?> GetByProviderAndSubjectAsync(string providerId, string externalSubject, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalUserLogin>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<ExternalUserLogin> AddAsync(ExternalUserLogin login, CancellationToken ct = default);
    Task<ExternalUserLogin> UpdateAsync(ExternalUserLogin login, CancellationToken ct = default);
}
