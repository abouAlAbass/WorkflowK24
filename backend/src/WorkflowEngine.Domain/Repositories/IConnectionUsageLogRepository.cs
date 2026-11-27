using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IConnectionUsageLogRepository
{
    Task<IReadOnlyList<ConnectionUsageLog>> GetByConnectionIdAsync(string connectionId, int limit = 50, CancellationToken ct = default);
    void Add(ConnectionUsageLog log);
}
