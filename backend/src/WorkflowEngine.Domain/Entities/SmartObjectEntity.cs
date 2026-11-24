namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a Smart Object schema definition (entity) in the workflow data model.
/// </summary>
public class SmartObjectEntity
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = "Custom";
    public int RecordsCount { get; private set; }
    public string FieldsJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SmartObjectEntity() { } // EF Core

    public SmartObjectEntity(
        string id,
        string name,
        string displayName,
        string description,
        string category,
        string fieldsJson,
        int recordsCount = 0)
    {
        Id = string.IsNullOrWhiteSpace(id) ? $"so-{Guid.NewGuid():N}" : id;
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name is required", nameof(name));
        DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : name;
        Description = description ?? string.Empty;
        Category = !string.IsNullOrWhiteSpace(category) ? category : "Custom";
        FieldsJson = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : "[]";
        RecordsCount = Math.Max(0, recordsCount);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateMetadata(string displayName, string description, string category, string? fieldsJson = null)
    {
        DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : DisplayName;
        Description = description ?? Description;
        Category = !string.IsNullOrWhiteSpace(category) ? category : Category;
        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            FieldsJson = fieldsJson;
        }
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
