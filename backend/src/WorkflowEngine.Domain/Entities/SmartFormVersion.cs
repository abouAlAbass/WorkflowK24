using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Versioned snapshot of a SmartForm's field schema.
/// Once Published or Archived, FieldsJson is strictly immutable.
/// </summary>
public class SmartFormVersion
{
    public string Id { get; private set; } = string.Empty;
    public string SmartFormId { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public SmartFormVersionStatus Status { get; private set; } = SmartFormVersionStatus.Draft;
    public string FieldsJson { get; private set; } = "[]";
    public DateTime? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public SmartFormEntity SmartForm { get; private set; } = default!;

    private SmartFormVersion() { } // EF Core

    public SmartFormVersion(
        string smartFormId,
        int versionNumber,
        string fieldsJson,
        SmartFormVersionStatus status = SmartFormVersionStatus.Draft,
        string? publishedBy = null,
        DateTime? publishedAt = null,
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(smartFormId))
            throw new ArgumentException("SmartFormId is required.", nameof(smartFormId));
        if (versionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "VersionNumber must be >= 1.");

        Id = string.IsNullOrWhiteSpace(id) ? $"sfv-{Guid.NewGuid():N}"[..24] : id;
        SmartFormId = smartFormId;
        VersionNumber = versionNumber;
        FieldsJson = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : "[]";
        Status = status;
        CreatedAt = DateTime.UtcNow;

        if (status == SmartFormVersionStatus.Published)
        {
            PublishedBy = !string.IsNullOrWhiteSpace(publishedBy) ? publishedBy : "system";
            PublishedAt = publishedAt ?? CreatedAt;
        }
    }

    public void UpdateFields(string fieldsJson)
    {
        if (Status != SmartFormVersionStatus.Draft)
        {
            throw new SmartFormVersionImmutableException(
                $"{SmartFormVersionImmutableException.ErrorCode}: SmartFormVersion '{Id}' (v{VersionNumber}) has status '{Status}' and is strictly immutable.");
        }

        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            FieldsJson = fieldsJson;
        }
    }

    public void Publish(string publishedBy)
    {
        if (Status != SmartFormVersionStatus.Draft)
        {
            throw new SmartFormVersionImmutableException(
                $"{SmartFormVersionImmutableException.ErrorCode}: SmartFormVersion '{Id}' (v{VersionNumber}) has status '{Status}' and cannot be published again.");
        }

        Status = SmartFormVersionStatus.Published;
        PublishedBy = !string.IsNullOrWhiteSpace(publishedBy) ? publishedBy : "system";
        PublishedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == SmartFormVersionStatus.Published)
        {
            Status = SmartFormVersionStatus.Archived;
        }
    }
}
