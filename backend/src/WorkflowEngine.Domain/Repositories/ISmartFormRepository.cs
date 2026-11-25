using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ISmartFormRepository
{
    Task<IReadOnlyList<SmartFormEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SmartFormEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(SmartFormEntity form, CancellationToken ct = default);
    Task UpdateAsync(SmartFormEntity form, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    Task AddSubmissionAsync(SmartFormSubmissionEntity submission, CancellationToken ct = default);
    Task<IReadOnlyList<SmartFormSubmissionEntity>> GetSubmissionsAsync(string smartFormId, CancellationToken ct = default);
    Task<SmartFormSubmissionEntity?> GetSubmissionByIdempotencyKeyAsync(string smartFormId, string idempotencyKey, CancellationToken ct = default);

    Task AddAttachmentAsync(FormAttachmentEntity attachment, CancellationToken ct = default);
    Task<FormAttachmentEntity?> GetAttachmentByIdAsync(string attachmentId, CancellationToken ct = default);
    Task UpdateAttachmentAsync(FormAttachmentEntity attachment, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string attachmentId, CancellationToken ct = default);
}
