using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Repository contract for managing reusable external Connection definitions.
/// </summary>
public interface IConnectionRepository
{
    Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct = default);
    Task<Connection?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Connection?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Connection?> GetByIdOrNameAsync(string idOrName, CancellationToken ct = default);
    void Add(Connection connection);
    void Remove(Connection connection);
}
