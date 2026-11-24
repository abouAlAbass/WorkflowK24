using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ISystemUserRepository
{
    Task<IReadOnlyList<SystemUser>> GetAllAsync(CancellationToken ct = default);
    Task<SystemUser?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SystemUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<SystemUser> AddAsync(SystemUser user, CancellationToken ct = default);
    Task<SystemUser> UpdateAsync(SystemUser user, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
