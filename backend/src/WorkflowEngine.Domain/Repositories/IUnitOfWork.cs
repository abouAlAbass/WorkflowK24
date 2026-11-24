namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for coordinating transactional persistence across domain repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
