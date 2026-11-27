using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ConnectionRepository : IConnectionRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ConnectionRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Connections
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Connection?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Connections
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Connection?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Connections
            .FirstOrDefaultAsync(c => c.Name == name, ct);
    }

    public async Task<Connection?> GetByIdOrNameAsync(string idOrName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        string key = idOrName.Trim();
        return await _context.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == key || c.Name == key, ct);
    }

    public void Add(Connection connection)
    {
        _context.Connections.Add(connection);
    }

    public void Remove(Connection connection)
    {
        _context.Connections.Remove(connection);
    }
}
