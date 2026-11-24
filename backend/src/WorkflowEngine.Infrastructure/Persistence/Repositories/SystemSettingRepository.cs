using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly WorkflowEngineDbContext _context;

    public SystemSettingRepository(WorkflowEngineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SystemSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(ct);
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, ct);
    }

    public async Task<SystemSetting> SetAsync(string key, string value, string category = "General", CancellationToken ct = default)
    {
        var existing = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing != null)
        {
            existing.UpdateValue(value);
            return existing;
        }

        var created = new SystemSetting(key, value, category);
        await _context.SystemSettings.AddAsync(created, ct);
        return created;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
