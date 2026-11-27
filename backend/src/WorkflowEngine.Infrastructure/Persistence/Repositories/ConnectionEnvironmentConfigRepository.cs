using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ConnectionEnvironmentConfigRepository : IConnectionEnvironmentConfigRepository
{
    private readonly WorkflowEngineDbContext _context;

    public ConnectionEnvironmentConfigRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ConnectionEnvironmentConfig>> GetByConnectionIdAsync(
        string connectionId,
        CancellationToken ct = default)
    {
        return await _context.ConnectionEnvironmentConfigs
            .AsNoTracking()
            .Where(e => e.ConnectionId == connectionId)
            .OrderBy(e => e.EnvironmentName)
            .ToListAsync(ct);
    }

    public async Task<ConnectionEnvironmentConfig?> GetByConnectionAndEnvironmentAsync(
        string connectionId,
        string environmentName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(environmentName))
            return null;

        string canonical = ConnectionEnvironmentConfig.CanonicalEnvironmentNames.TryGetValue(environmentName.Trim(), out var c)
            ? c
            : environmentName.Trim();

        return await _context.ConnectionEnvironmentConfigs
            .FirstOrDefaultAsync(e => e.ConnectionId == connectionId && e.EnvironmentName == canonical, ct);
    }

    public void Add(ConnectionEnvironmentConfig config)
    {
        _context.ConnectionEnvironmentConfigs.Add(config);
    }

    public void Remove(ConnectionEnvironmentConfig config)
    {
        _context.ConnectionEnvironmentConfigs.Remove(config);
    }
}
