using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class SmartFormRepository : ISmartFormRepository
{
    private readonly WorkflowEngineDbContext _context;

    public SmartFormRepository(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SmartFormEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SmartForms
            .AsNoTracking()
            .Include(f => f.Versions)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<SmartFormEntity?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.SmartForms
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<SmartFormVersion>> GetVersionsAsync(string smartFormId, CancellationToken ct = default)
    {
        return await _context.SmartFormVersions
            .AsNoTracking()
            .Where(v => v.SmartFormId == smartFormId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<SmartFormVersion?> GetVersionByIdAsync(string smartFormId, string versionId, CancellationToken ct = default)
    {
        return await _context.SmartFormVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.SmartFormId == smartFormId && v.Id == versionId, ct);
    }

    public async Task AddAsync(SmartFormEntity form, CancellationToken ct = default)
    {
        await _context.SmartForms.AddAsync(form, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SmartFormEntity form, CancellationToken ct = default)
    {
        var entry = _context.Entry(form);
        if (entry.State == EntityState.Detached)
        {
            _context.SmartForms.Update(form);
        }

        foreach (var version in form.Versions)
        {
            var versionEntry = _context.Entry(version);
            if (versionEntry.State == EntityState.Detached)
            {
                await _context.SmartFormVersions.AddAsync(version, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var form = await _context.SmartForms.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (form == null) return false;

        var submissions = await _context.SmartFormSubmissions
            .Where(s => s.SmartFormId == id)
            .ToListAsync(ct);

        if (submissions.Count > 0)
        {
            _context.SmartFormSubmissions.RemoveRange(submissions);
        }

        _context.SmartForms.Remove(form);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task AddSubmissionAsync(SmartFormSubmissionEntity submission, CancellationToken ct = default)
    {
        await _context.SmartFormSubmissions.AddAsync(submission, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SmartFormSubmissionEntity>> GetSubmissionsAsync(string smartFormId, CancellationToken ct = default)
    {
        return await _context.SmartFormSubmissions
            .AsNoTracking()
            .Where(s => s.SmartFormId == smartFormId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<SmartFormSubmissionEntity?> GetSubmissionByIdempotencyKeyAsync(string smartFormId, string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.SmartFormSubmissions
            .FirstOrDefaultAsync(s => s.SmartFormId == smartFormId && s.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task AddAttachmentAsync(FormAttachmentEntity attachment, CancellationToken ct = default)
    {
        await _context.FormAttachments.AddAsync(attachment, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<FormAttachmentEntity?> GetAttachmentByIdAsync(string attachmentId, CancellationToken ct = default)
    {
        return await _context.FormAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
    }

    public async Task UpdateAttachmentAsync(FormAttachmentEntity attachment, CancellationToken ct = default)
    {
        _context.FormAttachments.Update(attachment);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAttachmentAsync(string attachmentId, CancellationToken ct = default)
    {
        var entity = await _context.FormAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (entity == null) return false;

        _context.FormAttachments.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
