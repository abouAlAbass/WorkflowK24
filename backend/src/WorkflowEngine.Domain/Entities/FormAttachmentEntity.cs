namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Metadata record for a securely stored file attachment uploaded through a SmartForm,
/// enforcing ownership and optional workflow instance / submission binding.
/// </summary>
public class FormAttachmentEntity
{
    public string Id { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = "application/octet-stream";
    public long SizeBytes { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string Sha256Hash { get; private set; } = string.Empty;
    public string UploadedByUserId { get; private set; } = string.Empty;
    public string? WorkflowInstanceId { get; private set; }
    public string? SubmissionId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private FormAttachmentEntity() { } // EF Core

    public FormAttachmentEntity(
        string id,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        string sha256Hash,
        string uploadedByUserId,
        string? workflowInstanceId = null,
        string? submissionId = null)
    {
        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new ArgumentException("UploadedByUserId is required.", nameof(uploadedByUserId));
        }

        Id = string.IsNullOrWhiteSpace(id) ? $"att-{Guid.NewGuid():N}" : id;
        FileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : "attachment.bin";
        ContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType : "application/octet-stream";
        SizeBytes = Math.Max(0, sizeBytes);
        StoragePath = !string.IsNullOrWhiteSpace(storagePath)
            ? storagePath
            : throw new ArgumentException("StoragePath is required", nameof(storagePath));
        Sha256Hash = sha256Hash ?? string.Empty;
        UploadedByUserId = uploadedByUserId.Trim();
        WorkflowInstanceId = string.IsNullOrWhiteSpace(workflowInstanceId) ? null : workflowInstanceId.Trim();
        SubmissionId = string.IsNullOrWhiteSpace(submissionId) ? null : submissionId.Trim();
        UploadedAt = DateTime.UtcNow;
    }

    public void BindToWorkflowSubmission(string? submissionId, string? workflowInstanceId)
    {
        if (!string.IsNullOrWhiteSpace(submissionId))
        {
            SubmissionId = submissionId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            WorkflowInstanceId = workflowInstanceId.Trim();
        }
    }
}
