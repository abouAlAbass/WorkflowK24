using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Unified security audit service recording typed events with strict secret-leakage prevention.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records a typed security audit event. Any metadata key containing forbidden secret keywords
    /// (<c>password</c>, <c>secret</c>, <c>token</c>, <c>connectionString</c>, <c>authorization</c>)
    /// is automatically stripped before persistence.
    /// </summary>
    Task<SecurityAuditEvent> RecordAsync(
        SecurityAuditEventType eventType,
        string actorId,
        string targetType,
        string targetId,
        IDictionary<string, object?>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Records a typed security audit event from a raw JSON object string after recursively stripping
    /// forbidden secret keys.
    /// </summary>
    Task<SecurityAuditEvent> RecordAsync(
        SecurityAuditEventType eventType,
        string actorId,
        string targetType,
        string targetId,
        string? metadataJson,
        CancellationToken ct = default);

    /// <summary>
    /// Queries recent security audit events, optionally filtered by event type.
    /// </summary>
    Task<IReadOnlyList<SecurityAuditEvent>> GetRecentAsync(
        int limit = 100,
        SecurityAuditEventType? eventType = null,
        CancellationToken ct = default);
}
