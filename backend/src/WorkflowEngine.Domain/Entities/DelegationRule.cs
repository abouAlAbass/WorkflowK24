namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Out-of-Office / temporary delegation rule automatically forwarding new tasks
/// assigned to DelegatorId to DelegateId during [ValidFrom, ValidTo].
/// </summary>
public class DelegationRule
{
    public string Id { get; private set; } = default!;
    public string DelegatorId { get; private set; } = default!;
    public string DelegateId { get; private set; } = default!;
    public string Scope { get; private set; } = "*";
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }
    public bool Active { get; private set; } = true;
    public string? Reason { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private DelegationRule() { } // EF Core

    public DelegationRule(
        string id,
        string delegatorId,
        string delegateId,
        string? scope,
        DateTime validFrom,
        DateTime validTo,
        string createdBy,
        string? reason = null,
        bool active = true)
    {
        Id = !string.IsNullOrWhiteSpace(id) ? id.Trim() : $"del-{Guid.NewGuid():N}";
        DelegatorId = !string.IsNullOrWhiteSpace(delegatorId)
            ? delegatorId.Trim()
            : throw new ArgumentException("DelegatorId is required.", nameof(delegatorId));
        DelegateId = !string.IsNullOrWhiteSpace(delegateId)
            ? delegateId.Trim()
            : throw new ArgumentException("DelegateId is required.", nameof(delegateId));
        Scope = string.IsNullOrWhiteSpace(scope) ? "*" : scope.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
        Active = active;
        Reason = reason;
        CreatedBy = !string.IsNullOrWhiteSpace(createdBy) ? createdBy.Trim() : DelegatorId;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsEffectiveAt(DateTime utcNow, string? workflowKey = null)
    {
        if (!Active) return false;
        if (utcNow < ValidFrom || utcNow > ValidTo) return false;
        if (string.Equals(Scope, "*", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(workflowKey)) return false;
        return string.Equals(Scope, workflowKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public void Deactivate()
    {
        Active = false;
    }
}
