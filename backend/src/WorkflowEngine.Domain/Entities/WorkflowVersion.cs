namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Immutable snapshot version of a workflow graph definition.
/// </summary>
public class WorkflowVersion
{
    public int VersionId { get; private set; }
    public int DefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public string GraphJson { get; private set; } = default!;
    public string? Changelog { get; private set; }
    public bool IsActive { get; private set; }
    public string PublishedBy { get; private set; } = default!;
    public DateTime PublishedAt { get; private set; } = DateTime.UtcNow;

    public WorkflowDefinition Definition { get; private set; } = default!;

    private WorkflowVersion() { } // Required for ORM hydration

    public WorkflowVersion(int definitionId, int versionNumber, string graphJson, string publishedBy, string? changelog, bool isActive)
    {
        DefinitionId = definitionId;
        VersionNumber = versionNumber;
        GraphJson = graphJson ?? throw new ArgumentNullException(nameof(graphJson));
        PublishedBy = publishedBy ?? throw new ArgumentNullException(nameof(publishedBy));
        Changelog = changelog;
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
