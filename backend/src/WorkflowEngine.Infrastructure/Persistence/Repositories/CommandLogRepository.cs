using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class CommandLogRepository : ICommandLogRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public CommandLogRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CommandLog?> GetByIdAsync(Guid commandId, CancellationToken ct = default)
    {
        return await _dbContext.CommandLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommandId == commandId, ct);
    }

    public void Add(CommandLog commandLog)
    {
        _dbContext.CommandLogs.Add(commandLog);
    }
}
