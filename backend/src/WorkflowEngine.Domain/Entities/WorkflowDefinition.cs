namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Root aggregate representing a workflow definition template in the registry.
/// </summary>
public class WorkflowDefinition
{
    public int DefinitionId { get; private set; }
    public string WorkflowKey { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Category { get; private set; } = "General";
    public string CreatedBy { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private readonly List<WorkflowVersion> _versions = new();
    public IReadOnlyCollection<WorkflowVersion> Versions => _versions.AsReadOnly();

    private WorkflowDefinition() { } // Required for ORM hydration

    public WorkflowDefinition(string workflowKey, string name, string createdBy, string? description = null, string category = "General")
    {
        WorkflowKey = workflowKey ?? throw new ArgumentNullException(nameof(workflowKey));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        Description = description;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
    }

    public WorkflowVersion AddVersion(string graphJson, string publishedBy, string? changelog = null, bool setActive = true)
    {
        int nextVersionNumber = _versions.Count > 0 ? _versions.Max(v => v.VersionNumber) + 1 : 1;

        if (setActive)
        {
            foreach (var version in _versions)
            {
                version.Deactivate();
            }
        }

        var newVersion = new WorkflowVersion(DefinitionId, nextVersionNumber, graphJson, publishedBy, changelog, setActive);
        _versions.Add(newVersion);
        UpdatedAt = DateTime.UtcNow;
        return newVersion;
    }
}
