using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ISystemUserRepository
{
    Task<IReadOnlyList<SystemUser>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SystemUser>> SearchAsync(string? query, int limit, CancellationToken ct = default);
    Task<SystemUser?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SystemUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<SystemUser> AddAsync(SystemUser user, CancellationToken ct = default);
    Task<SystemUser> UpdateAsync(SystemUser user, CancellationToken ct = default);
    /// <summary>True when the user has pending tasks assigned or has initiated workflow instances.</summary>
    Task<bool> HasLinkedWorkAsync(string userId, string email, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
