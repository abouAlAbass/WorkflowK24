using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ICommandLogRepository
{
    Task<CommandLog?> GetByIdAsync(Guid commandId, CancellationToken ct = default);
    void Add(CommandLog commandLog);
}
