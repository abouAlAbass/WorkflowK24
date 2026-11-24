using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Repository port for managing Smart Objects and their underlying data records.
/// </summary>
public interface ISmartObjectRepository
{
    Task<IReadOnlyList<SmartObjectEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SmartObjectEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SmartObjectEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<SmartObjectEntity> AddAsync(SmartObjectEntity entity, CancellationToken ct = default);
    Task<SmartObjectEntity> UpdateAsync(SmartObjectEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<SmartObjectRecordEntity>> GetRecordsAsync(string smartObjectId, CancellationToken ct = default);
    Task<SmartObjectRecordEntity?> GetRecordByKeyAsync(string smartObjectId, string recordKey, CancellationToken ct = default);
    Task<SmartObjectRecordEntity> AddRecordAsync(SmartObjectRecordEntity record, CancellationToken ct = default);
    Task<SmartObjectRecordEntity> UpdateRecordAsync(SmartObjectRecordEntity record, CancellationToken ct = default);
    Task<bool> DeleteRecordAsync(string smartObjectId, string recordKey, CancellationToken ct = default);
}
