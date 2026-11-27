using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Repository contract for per-user encrypted connection credentials (personal vault).
/// </summary>
public interface IUserConnectionCredentialRepository
{
    Task<IReadOnlyList<UserConnectionCredential>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserConnectionCredential?> GetByUserAndConnectionAsync(string userId, string connectionId, CancellationToken ct = default);
    Task<UserConnectionCredential?> GetByAnyUserIdAndConnectionAsync(IEnumerable<string> userIds, string connectionId, CancellationToken ct = default);
    void Add(UserConnectionCredential credential);
    void Remove(UserConnectionCredential credential);
}
