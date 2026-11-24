namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a persistent key-value configuration item in the system.
/// </summary>
public class SystemSetting
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string Category { get; private set; } = "General";
    public DateTime UpdatedAt { get; private set; }

    private SystemSetting() { } // EF Core

    public SystemSetting(string key, string value, string category = "General")
    {
        Key = !string.IsNullOrWhiteSpace(key) ? key : throw new ArgumentException("Key is required", nameof(key));
        Value = value ?? string.Empty;
        Category = !string.IsNullOrWhiteSpace(category) ? category : "General";
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateValue(string value)
    {
        Value = value ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
}
