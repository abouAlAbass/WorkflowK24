namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for coordinating transactional persistence across domain repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a single database transaction (retry-strategy aware).
    /// Any exception rolls back every write performed by the delegate and is rethrown to the caller.
    /// The delegate may be re-executed on transient failures, so it must load its own state.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
