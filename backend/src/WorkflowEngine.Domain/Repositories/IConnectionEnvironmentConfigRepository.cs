using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IConnectionEnvironmentConfigRepository
{
    Task<IReadOnlyList<ConnectionEnvironmentConfig>> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default);
    Task<ConnectionEnvironmentConfig?> GetByConnectionAndEnvironmentAsync(string connectionId, string environmentName, CancellationToken ct = default);
    void Add(ConnectionEnvironmentConfig config);
    void Remove(ConnectionEnvironmentConfig config);
}
