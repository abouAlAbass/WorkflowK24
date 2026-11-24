using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct = default);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<SystemSetting> SetAsync(string key, string value, string category = "General", CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
