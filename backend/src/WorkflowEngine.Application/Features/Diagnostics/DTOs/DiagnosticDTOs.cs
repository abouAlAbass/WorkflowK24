namespace WorkflowEngine.Application.Features.Diagnostics.DTOs;

public record FailingWorkflowDiagnosticDto(
    string InstanceId,
    string WorkflowKey,
    string FailingNodeId,
    string FailingNodeType,
    string ErrorCode,
    string ErrorMessage,
    string? StackTrace,
    string? InputSnapshotJson,
    DateTime OccurredAt,
    bool IsResolved
);

public record AuditLogDto(
    long LogId,
    string InstanceId,
    int SequenceNumber,
    string? NodeId,
    string? NodeType,
    string EventType,
    string EventSummary,
    string? OutcomeBranch,
    string? StateSnapshotJson,
    DateTime LoggedAt,
    string? ActorUserId = null,
    string? BeforeJson = null,
    string? AfterJson = null
);
