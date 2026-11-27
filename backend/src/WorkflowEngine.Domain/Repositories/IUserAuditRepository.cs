using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IUserAuditRepository
{
    Task AddAsync(UserAuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<UserAuditLog>> GetRecentAsync(int limit, CancellationToken ct = default);
}
