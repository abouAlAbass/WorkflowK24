using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Repository contract for fine-grained ConnectionPermission governance rules.
/// </summary>
public interface IConnectionPermissionRepository
{
    Task<IReadOnlyList<ConnectionPermission>> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectionPermission>> GetAllAsync(CancellationToken ct = default);
    Task<ConnectionPermission?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ConnectionPermission?> GetBySubjectAsync(string connectionId, string subjectType, string subjectId, CancellationToken ct = default);
    void Add(ConnectionPermission permission);
    void Remove(ConnectionPermission permission);
}
