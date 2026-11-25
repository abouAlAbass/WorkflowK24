namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a runtime user submission of a SmartForm, optionally linked to a WorkflowInstance.
/// </summary>
public class SmartFormSubmissionEntity
{
    public string Id { get; private set; } = string.Empty;
    public string SmartFormId { get; private set; } = string.Empty;
    public string? SmartFormVersionId { get; private set; }
    public string SubmittedBy { get; private set; } = string.Empty;
    public string DataJson { get; private set; } = "{}";
    public string? LinkedInstanceId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime SubmittedAt { get; private set; }

    private SmartFormSubmissionEntity() { } // EF Core

    public SmartFormSubmissionEntity(
        string id,
        string smartFormId,
        string submittedBy,
        string dataJson,
        string? linkedInstanceId = null,
        string? idempotencyKey = null,
        string? smartFormVersionId = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? $"sub-{Guid.NewGuid():N}" : id;
        SmartFormId = !string.IsNullOrWhiteSpace(smartFormId) ? smartFormId : throw new ArgumentException("SmartFormId is required", nameof(smartFormId));
        SmartFormVersionId = string.IsNullOrWhiteSpace(smartFormVersionId) ? null : smartFormVersionId.Trim();
        SubmittedBy = !string.IsNullOrWhiteSpace(submittedBy) ? submittedBy : "anonymous";
        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        LinkedInstanceId = linkedInstanceId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        SubmittedAt = DateTime.UtcNow;
    }

    public void LinkWorkflowInstance(string instanceId)
    {
        LinkedInstanceId = !string.IsNullOrWhiteSpace(instanceId)
            ? instanceId
            : throw new ArgumentException("InstanceId is required", nameof(instanceId));
    }
}
