namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a No-Code SmartForm schema definition bound to SmartObjects and Workflows.
/// </summary>
public class SmartFormEntity
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = "General";
    public string? LinkedSmartObjectId { get; private set; }
    public string? LinkedWorkflowKey { get; private set; }
    public string Status { get; private set; } = "Published";
    public string FieldsJson { get; private set; } = "[]";
    public int SubmissionsCount { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Incremented on every definition edit (schema/metadata/bindings),
    /// but NOT on submission counter increments so runtime traffic never invalidates a designer's copy.
    /// </summary>
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SmartFormEntity() { } // EF Core

    public SmartFormEntity(
        string id,
        string name,
        string title,
        string description,
        string category,
        string fieldsJson,
        string? linkedSmartObjectId = null,
        string? linkedWorkflowKey = null,
        string status = "Published")
    {
        Id = string.IsNullOrWhiteSpace(id) ? $"form-{Guid.NewGuid():N}" : id;
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name is required", nameof(name));
        Title = !string.IsNullOrWhiteSpace(title) ? title : name;
        Description = description ?? string.Empty;
        Category = !string.IsNullOrWhiteSpace(category) ? category : "General";
        FieldsJson = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : "[]";
        LinkedSmartObjectId = string.IsNullOrWhiteSpace(linkedSmartObjectId) ? null : linkedSmartObjectId;
        LinkedWorkflowKey = string.IsNullOrWhiteSpace(linkedWorkflowKey) ? null : linkedWorkflowKey;
        Status = !string.IsNullOrWhiteSpace(status) ? status : "Published";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSchema(
        string title,
        string description,
        string category,
        string fieldsJson,
        string? linkedSmartObjectId,
        string? linkedWorkflowKey,
        string status)
    {
        Title = !string.IsNullOrWhiteSpace(title) ? title : Title;
        Description = description ?? Description;
        Category = !string.IsNullOrWhiteSpace(category) ? category : Category;
        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            FieldsJson = fieldsJson;
        }
        LinkedSmartObjectId = string.IsNullOrWhiteSpace(linkedSmartObjectId) ? null : linkedSmartObjectId;
        LinkedWorkflowKey = string.IsNullOrWhiteSpace(linkedWorkflowKey) ? null : linkedWorkflowKey;
        Status = !string.IsNullOrWhiteSpace(status) ? status : Status;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementSubmissions()
    {
        SubmissionsCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
