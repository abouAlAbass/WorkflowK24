namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a runtime user submission of a SmartForm, optionally linked to a WorkflowInstance
/// and a canonical SmartObjectRecord to avoid unlinked data duplication.
/// </summary>
public class SmartFormSubmissionEntity
{
    public const string StatusDraft = "Draft";
    public const string StatusSubmitted = "Submitted";

    public string Id { get; private set; } = string.Empty;
    public string SmartFormId { get; private set; } = string.Empty;
    public string? SmartFormVersionId { get; private set; }
    public string SubmittedBy { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusSubmitted;
    public string DataJson { get; private set; } = "{}";
    public string? LinkedInstanceId { get; private set; }
    public long? SmartObjectRecordId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SmartFormSubmissionEntity() { } // EF Core

    public SmartFormSubmissionEntity(
        string id,
        string smartFormId,
        string submittedBy,
        string dataJson,
        string? linkedInstanceId = null,
        string? idempotencyKey = null,
        string? smartFormVersionId = null,
        long? smartObjectRecordId = null,
        string status = StatusSubmitted)
    {
        Id = string.IsNullOrWhiteSpace(id) ? $"sub-{Guid.NewGuid():N}" : id;
        SmartFormId = !string.IsNullOrWhiteSpace(smartFormId) ? smartFormId : throw new ArgumentException("SmartFormId is required", nameof(smartFormId));
        SmartFormVersionId = string.IsNullOrWhiteSpace(smartFormVersionId) ? null : smartFormVersionId.Trim();
        SubmittedBy = !string.IsNullOrWhiteSpace(submittedBy) ? submittedBy : "anonymous";
        Status = string.Equals(status, StatusDraft, StringComparison.OrdinalIgnoreCase) ? StatusDraft : StatusSubmitted;
        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        LinkedInstanceId = linkedInstanceId;
        SmartObjectRecordId = smartObjectRecordId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = SubmittedAt;
    }

    public bool IsDraft => string.Equals(Status, StatusDraft, StringComparison.OrdinalIgnoreCase);

    public void UpdateDraftData(string dataJson, string? smartFormVersionId = null)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("Cannot update draft data on a submission that has already been submitted.");
        }

        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        if (!string.IsNullOrWhiteSpace(smartFormVersionId))
        {
            SmartFormVersionId = smartFormVersionId.Trim();
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteDraft(
        string dataJson,
        string? smartFormVersionId,
        long? smartObjectRecordId,
        string? idempotencyKey)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("Submission has already been submitted.");
        }

        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        SmartFormVersionId = string.IsNullOrWhiteSpace(smartFormVersionId) ? SmartFormVersionId : smartFormVersionId.Trim();
        SmartObjectRecordId = smartObjectRecordId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        Status = StatusSubmitted;
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = SubmittedAt;
    }

    public void LinkWorkflowInstance(string instanceId)
    {
        LinkedInstanceId = !string.IsNullOrWhiteSpace(instanceId)
            ? instanceId
            : throw new ArgumentException("InstanceId is required", nameof(instanceId));
    }

    public void LinkSmartObjectRecord(long recordId)
    {
        if (recordId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordId), "SmartObjectRecordId must be positive.");
        }

        SmartObjectRecordId = recordId;
    }
}
