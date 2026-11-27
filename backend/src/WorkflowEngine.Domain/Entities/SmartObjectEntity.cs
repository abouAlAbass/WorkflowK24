using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a versioned Smart Object schema &amp; method contract (ObjectKey@Version, e.g. Customer@2).
/// Draft versions are mutable; Published and Archived versions are strictly immutable.
/// </summary>
public class SmartObjectEntity
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;
    public SmartObjectStatus Status { get; private set; } = SmartObjectStatus.Draft;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = "Custom";
    public int RecordsCount { get; private set; }
    public string FieldsJson { get; private set; } = "[]";
    public string MethodsJson { get; private set; } = "[]";
    public string? SourceConnectionId { get; private set; }
    public string? SourceEntityName { get; private set; }
    public string? PrimaryKeyField { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string ObjectRef => $"{Name}@{Version}";

    private SmartObjectEntity() { } // EF Core

    public SmartObjectEntity(
        string id,
        string name,
        string displayName,
        string description,
        string category,
        string fieldsJson,
        int recordsCount = 0,
        int version = 1,
        SmartObjectStatus status = SmartObjectStatus.Draft,
        string? methodsJson = null,
        string? publishedBy = null,
        string? sourceConnectionId = null,
        string? sourceEntityName = null,
        string? primaryKeyField = null)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1.");
        }

        Id = string.IsNullOrWhiteSpace(id) ? $"so-{Guid.NewGuid():N}" : id;
        Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : throw new ArgumentException("Name is required", nameof(name));
        Version = version;
        Status = status;
        DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : Name;
        Description = description ?? string.Empty;
        Category = !string.IsNullOrWhiteSpace(category) ? category : "Custom";
        FieldsJson = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : "[]";
        MethodsJson = !string.IsNullOrWhiteSpace(methodsJson) ? methodsJson : "[]";
        SourceConnectionId = string.IsNullOrWhiteSpace(sourceConnectionId) ? null : sourceConnectionId.Trim();
        SourceEntityName = string.IsNullOrWhiteSpace(sourceEntityName) ? null : sourceEntityName.Trim();
        PrimaryKeyField = string.IsNullOrWhiteSpace(primaryKeyField) ? null : primaryKeyField.Trim();
        RecordsCount = Math.Max(0, recordsCount);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (status == SmartObjectStatus.Published)
        {
            PublishedAt = DateTime.UtcNow;
            PublishedBy = string.IsNullOrWhiteSpace(publishedBy) ? "system" : publishedBy.Trim();
        }
    }

    public void UpdateMetadata(
        string displayName,
        string description,
        string category,
        string? fieldsJson = null,
        string? methodsJson = null)
    {
        if (Status != SmartObjectStatus.Draft)
        {
            throw new InvalidOperationException(
                $"SmartObject '{ObjectRef}' has status '{Status}' and is immutable. Create a new Draft version to modify it.");
        }

        DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : DisplayName;
        Description = description ?? Description;
        Category = !string.IsNullOrWhiteSpace(category) ? category : Category;
        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            FieldsJson = fieldsJson;
        }
        if (!string.IsNullOrWhiteSpace(methodsJson))
        {
            MethodsJson = methodsJson;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplySchemaSync(string fieldsJson, string methodsJson, string? primaryKeyField)
    {
        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            FieldsJson = fieldsJson;
        }
        if (!string.IsNullOrWhiteSpace(methodsJson))
        {
            MethodsJson = methodsJson;
        }
        PrimaryKeyField = string.IsNullOrWhiteSpace(primaryKeyField) ? null : primaryKeyField.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish(string? publishedBy = null)
    {
        if (Status == SmartObjectStatus.Published)
        {
            return;
        }

        if (Status == SmartObjectStatus.Archived)
        {
            throw new InvalidOperationException($"Archived SmartObject '{ObjectRef}' cannot be re-published.");
        }

        Status = SmartObjectStatus.Published;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = string.IsNullOrWhiteSpace(publishedBy) ? "system" : publishedBy.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == SmartObjectStatus.Archived)
        {
            return;
        }

        Status = SmartObjectStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementRecordsCount()
    {
        RecordsCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecrementRecordsCount()
    {
        if (RecordsCount > 0) RecordsCount--;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRecordsCount(int count)
    {
        RecordsCount = Math.Max(0, count);
        UpdatedAt = DateTime.UtcNow;
    }
}
