namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a single stored data item (row) for a specific Smart Object.
/// </summary>
public class SmartObjectRecordEntity
{
    public long Id { get; private set; }
    public string SmartObjectId { get; private set; } = string.Empty;
    public string RecordKey { get; private set; } = string.Empty;
    public string DataJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SmartObjectRecordEntity() { } // EF Core

    public SmartObjectRecordEntity(string smartObjectId, string recordKey, string dataJson)
    {
        SmartObjectId = !string.IsNullOrWhiteSpace(smartObjectId) 
            ? smartObjectId 
            : throw new ArgumentException("SmartObjectId is required", nameof(smartObjectId));
        RecordKey = !string.IsNullOrWhiteSpace(recordKey) 
            ? recordKey 
            : throw new ArgumentException("RecordKey is required", nameof(recordKey));
        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateData(string dataJson)
    {
        DataJson = !string.IsNullOrWhiteSpace(dataJson) ? dataJson : "{}";
        UpdatedAt = DateTime.UtcNow;
    }
}
