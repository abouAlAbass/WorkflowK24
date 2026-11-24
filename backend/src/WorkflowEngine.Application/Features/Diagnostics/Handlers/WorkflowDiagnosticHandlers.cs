using WorkflowEngine.Application.Features.Diagnostics.DTOs;
using WorkflowEngine.Application.Features.Diagnostics.Queries;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Diagnostics.Handlers;

/// <summary>
/// Wolverine CQRS query handlers for diagnostic exception analysis and timeline audits.
/// </summary>
public class WorkflowDiagnosticHandlers
{
    private readonly IWorkflowErrorRepository _errorRepository;
    private readonly IWorkflowAuditRepository _auditRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;

    public WorkflowDiagnosticHandlers(
        IWorkflowErrorRepository errorRepository,
        IWorkflowAuditRepository auditRepository,
        IWorkflowInstanceRepository instanceRepository)
    {
        _errorRepository = errorRepository ?? throw new ArgumentNullException(nameof(errorRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
    }

    public async Task<FailingWorkflowDiagnosticDto?> Handle(GetFailingWorkflowQuery query, CancellationToken ct)
    {
        var error = await _errorRepository.GetLatestErrorByInstanceIdAsync(query.InstanceId, ct);
        if (error == null) return null;

        var instance = await _instanceRepository.GetByIdAsync(query.InstanceId, ct);

        return new FailingWorkflowDiagnosticDto(
            error.InstanceId,
            instance?.WorkflowKey ?? "Unknown",
            error.NodeId,
            error.NodeType,
            error.ErrorCode,
            error.ErrorMessage,
            error.StackTrace,
            error.InputSnapshotJson,
            error.OccurredAt,
            error.IsResolved
        );
    }

    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetInstanceTimelineQuery query, CancellationToken ct)
    {
        var logs = await _auditRepository.GetTimelineByInstanceIdAsync(query.InstanceId, ct);

        return logs.Select(l => new AuditLogDto(
            l.LogId,
            l.InstanceId,
            l.SequenceNumber,
            l.NodeId,
            l.NodeType,
            l.EventType.ToString(),
            l.EventSummary,
            l.OutcomeBranch,
            l.StateSnapshotJson,
            l.LoggedAt
        )).ToList();
    }
}
