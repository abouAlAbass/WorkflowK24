using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class SmartObjectRepository : ISmartObjectRepository
{
    private readonly WorkflowEngineDbContext _context;

    public SmartObjectRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SmartObjectEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SmartObjects
            .AsNoTracking()
            .OrderBy(s => s.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<SmartObjectEntity?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.SmartObjects
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<SmartObjectEntity?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.SmartObjects
            .FirstOrDefaultAsync(s => s.Name == name, ct);
    }

    public async Task<SmartObjectEntity> AddAsync(SmartObjectEntity entity, CancellationToken ct = default)
    {
        await _context.SmartObjects.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<SmartObjectEntity> UpdateAsync(SmartObjectEntity entity, CancellationToken ct = default)
    {
        _context.SmartObjects.Update(entity);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _context.SmartObjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity == null) return false;

        // Cascade delete records
        var records = await _context.SmartObjectRecords
            .Where(r => r.SmartObjectId == id)
            .ToListAsync(ct);

        _context.SmartObjectRecords.RemoveRange(records);
        _context.SmartObjects.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SmartObjectRecordEntity>> GetRecordsAsync(string smartObjectId, CancellationToken ct = default)
    {
        return await _context.SmartObjectRecords
            .AsNoTracking()
            .Where(r => r.SmartObjectId == smartObjectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<SmartObjectRecordEntity?> GetRecordByKeyAsync(string smartObjectId, string recordKey, CancellationToken ct = default)
    {
        return await _context.SmartObjectRecords
            .FirstOrDefaultAsync(r => r.SmartObjectId == smartObjectId && r.RecordKey == recordKey, ct);
    }

    public async Task<SmartObjectRecordEntity> AddRecordAsync(SmartObjectRecordEntity record, CancellationToken ct = default)
    {
        await _context.SmartObjectRecords.AddAsync(record, ct);
        var so = await _context.SmartObjects.FirstOrDefaultAsync(s => s.Id == record.SmartObjectId, ct);
        so?.IncrementRecordsCount();

        await _context.SaveChangesAsync(ct);
        return record;
    }

    public async Task<SmartObjectRecordEntity> UpdateRecordAsync(SmartObjectRecordEntity record, CancellationToken ct = default)
    {
        _context.SmartObjectRecords.Update(record);
        await _context.SaveChangesAsync(ct);
        return record;
    }

    public async Task<bool> DeleteRecordAsync(string smartObjectId, string recordKey, CancellationToken ct = default)
    {
        var record = await _context.SmartObjectRecords
            .FirstOrDefaultAsync(r => r.SmartObjectId == smartObjectId && r.RecordKey == recordKey, ct);

        if (record == null) return false;

        _context.SmartObjectRecords.Remove(record);
        var so = await _context.SmartObjects.FirstOrDefaultAsync(s => s.Id == smartObjectId, ct);
        so?.DecrementRecordsCount();

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
