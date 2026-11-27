using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a No-Code SmartForm schema definition bound to SmartObjects and Workflows,
/// backed by immutable SmartFormVersion snapshots (Draft -> Published -> Archived).
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
    public string Status { get; private set; } = "Draft";

    /// <summary>
    /// Backward-compatible schema projection: reflects the active Published version,
    /// or the current Draft version when no version has been published yet.
    /// </summary>
    public string FieldsJson { get; private set; } = "[]";
    public string RulesJson { get; private set; } = "[]";
    public int SubmissionsCount { get; private set; }

    public string? ActivePublishedVersionId { get; private set; }
    public int LatestVersionNumber { get; private set; } = 1;

    /// <summary>
    /// Optimistic concurrency token. Incremented on every definition edit (schema/metadata/bindings/publish),
    /// but NOT on submission counter increments so runtime traffic never invalidates a designer's copy.
    /// </summary>
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<SmartFormVersion> _versions = new();
    public IReadOnlyCollection<SmartFormVersion> Versions => _versions;

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
        string status = "Draft",
        bool publishImmediately = false,
        string? publishedBy = null,
        string? rulesJson = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? $"form-{Guid.NewGuid():N}" : id;
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name is required", nameof(name));
        Title = !string.IsNullOrWhiteSpace(title) ? title : name;
        Description = description ?? string.Empty;
        Category = !string.IsNullOrWhiteSpace(category) ? category : "General";
        FieldsJson = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : "[]";
        RulesJson = !string.IsNullOrWhiteSpace(rulesJson) ? rulesJson : "[]";
        LinkedSmartObjectId = string.IsNullOrWhiteSpace(linkedSmartObjectId) ? null : linkedSmartObjectId;
        LinkedWorkflowKey = string.IsNullOrWhiteSpace(linkedWorkflowKey) ? null : linkedWorkflowKey;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        bool startPublished = publishImmediately || string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase);
        var initialVersion = new SmartFormVersion(
            Id,
            versionNumber: 1,
            fieldsJson: FieldsJson,
            status: startPublished ? SmartFormVersionStatus.Published : SmartFormVersionStatus.Draft,
            publishedBy: startPublished ? (publishedBy ?? "system") : null,
            publishedAt: startPublished ? CreatedAt : null,
            rulesJson: RulesJson);

        _versions.Add(initialVersion);
        LatestVersionNumber = 1;
        ActivePublishedVersionId = startPublished ? initialVersion.Id : null;
        Status = startPublished ? "Published" : "Draft";
    }

    public SmartFormVersion? GetActivePublishedVersion()
    {
        if (!string.IsNullOrWhiteSpace(ActivePublishedVersionId))
        {
            var byId = _versions.FirstOrDefault(v => v.Id == ActivePublishedVersionId);
            if (byId != null && byId.Status == SmartFormVersionStatus.Published)
            {
                return byId;
            }
        }

        return _versions
            .Where(v => v.Status == SmartFormVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
    }

    public SmartFormVersion? GetCurrentDraftVersion()
    {
        return _versions
            .Where(v => v.Status == SmartFormVersionStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
    }

    public SmartFormVersion? GetLatestVersion()
    {
        return _versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
    }

    /// <summary>
    /// Updates metadata and edits the current Draft version.
    /// If no Draft version exists (e.g. the latest version is Published), automatically creates
    /// a new Draft version (LatestVersionNumber + 1) without ever mutating the Published version.
    /// </summary>
    public SmartFormVersion UpdateDraft(
        string title,
        string description,
        string category,
        string? fieldsJson,
        string? linkedSmartObjectId,
        string? linkedWorkflowKey,
        string? rulesJson = null)
    {
        Title = !string.IsNullOrWhiteSpace(title) ? title : Title;
        Description = description ?? Description;
        Category = !string.IsNullOrWhiteSpace(category) ? category : Category;
        LinkedSmartObjectId = string.IsNullOrWhiteSpace(linkedSmartObjectId) ? null : linkedSmartObjectId;
        LinkedWorkflowKey = string.IsNullOrWhiteSpace(linkedWorkflowKey) ? null : linkedWorkflowKey;

        var existingDraft = GetCurrentDraftVersion();
        SmartFormVersion targetDraft;

        if (existingDraft != null)
        {
            if (!string.IsNullOrWhiteSpace(fieldsJson) || rulesJson != null)
            {
                existingDraft.UpdateFields(fieldsJson, rulesJson);
            }
            targetDraft = existingDraft;
        }
        else
        {
            int nextNumber = Math.Max(LatestVersionNumber, _versions.Count > 0 ? _versions.Max(v => v.VersionNumber) : 0) + 1;
            string effectiveFields = !string.IsNullOrWhiteSpace(fieldsJson) ? fieldsJson : FieldsJson;
            string effectiveRules = rulesJson != null
                ? (string.IsNullOrWhiteSpace(rulesJson) ? "[]" : rulesJson)
                : RulesJson;
            targetDraft = new SmartFormVersion(Id, nextNumber, effectiveFields, SmartFormVersionStatus.Draft, rulesJson: effectiveRules);
            _versions.Add(targetDraft);
            LatestVersionNumber = nextNumber;
        }

        // Backward compatibility: FieldsJson & RulesJson reflect the active Published version if one exists,
        // otherwise the current Draft version.
        var activePublished = GetActivePublishedVersion();
        FieldsJson = activePublished != null ? activePublished.FieldsJson : targetDraft.FieldsJson;
        RulesJson = activePublished != null ? activePublished.RulesJson : targetDraft.RulesJson;
        Status = activePublished != null ? "Published" : "Draft";

        Version++;
        UpdatedAt = DateTime.UtcNow;
        return targetDraft;
    }

    /// <summary>
    /// Publishes the current Draft version, archives any previously Published version,
    /// and updates ActivePublishedVersionId, FieldsJson, and RulesJson.
    /// </summary>
    public SmartFormVersion PublishDraft(string publishedBy)
    {
        var draft = GetCurrentDraftVersion();
        if (draft == null)
        {
            throw new InvalidOperationException($"SmartForm '{Id}' has no Draft version to publish.");
        }

        foreach (var published in _versions.Where(v => v.Status == SmartFormVersionStatus.Published))
        {
            published.Archive();
        }

        draft.Publish(publishedBy);
        ActivePublishedVersionId = draft.Id;
        FieldsJson = draft.FieldsJson;
        RulesJson = draft.RulesJson;
        Status = "Published";
        Version++;
        UpdatedAt = DateTime.UtcNow;
        return draft;
    }

    /// <summary>
    /// Ensures legacy SmartForm rows created prior to immutable versioning have an initial Published v1 snapshot.
    /// </summary>
    public SmartFormVersion EnsureInitialPublishedVersion(string publishedBy = "system")
    {
        var existing = GetActivePublishedVersion() ?? GetLatestVersion();
        if (existing != null)
        {
            return existing;
        }

        var v1 = new SmartFormVersion(
            Id,
            versionNumber: 1,
            fieldsJson: FieldsJson,
            status: SmartFormVersionStatus.Published,
            publishedBy: publishedBy,
            publishedAt: UpdatedAt == default ? DateTime.UtcNow : UpdatedAt,
            rulesJson: RulesJson);

        _versions.Add(v1);
        LatestVersionNumber = 1;
        ActivePublishedVersionId = v1.Id;
        Status = "Published";
        return v1;
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
        UpdateDraft(title, description, category, fieldsJson, linkedSmartObjectId, linkedWorkflowKey);
    }

    public void IncrementSubmissions()
    {
        SubmissionsCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
