using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Tasks.DTOs;

public record TaskCompletedResult(
    string TaskId,
    string InstanceId,
    string Outcome,
    string? NextNodeId,
    string InstanceStatus,
    string? CompletedByUserId = null,
    string? OnBehalfOfUserId = null,
    bool IsTaskFullyCompleted = true
);

public record TaskSlotDto(
    long Id,
    string TaskId,
    string AssigneeUserId,
    string Status,
    string? Outcome,
    string? Comments,
    DateTime? ClaimedAt,
    DateTime? CompletedAt,
    string? CompletedByUserId,
    string? OnBehalfOfUserId
);

public record TaskAssignmentDto(
    long Id,
    string TaskId,
    string Action,
    string? FromAssigneeId,
    string ToAssigneeId,
    string ToAssigneeType,
    string ActorUserId,
    string? Reason,
    DateTime CreatedAt
);

public record DelegationRuleDto(
    string Id,
    string DelegatorId,
    string DelegateId,
    string Scope,
    DateTime ValidFrom,
    DateTime ValidTo,
    bool Active,
    string? Reason,
    string CreatedBy,
    DateTime CreatedAt
);

public record EscalationCheckResult(
    int EscalatedCount,
    IReadOnlyList<string> EscalatedTaskIds
);

public record WorkflowTaskDto(
    string TaskId,
    string InstanceId,
    string NodeId,
    string TaskTitle,
    string AssigneeType,
    string AssigneeId,
    string Status,
    string? SelectedOutcome,
    string? Comments,
    DateTime? DueDate,
    DateTime AssignedAt,
    DateTime? CompletedAt,
    string? CompletedBy,
    string? WorkflowName = null,
    string? InitiatorId = null,
    IReadOnlyList<ApprovalOutcomeDto>? Outcomes = null,
    string? FormId = null,
    string FormMode = "readonly",
    Dictionary<string, object?>? FormData = null,
    int Version = 1,
    string? FormVersionId = null,
    string? OriginalAssigneeId = null,
    string ResolutionPolicy = "Single",
    int? QuorumCount = null,
    string? ClaimedByUserId = null,
    DateTime? ClaimedAt = null,
    DateTime? DueAt = null,
    string? CompletedByUserId = null,
    string? OnBehalfOfUserId = null,
    DateTime? EscalatedAt = null,
    IReadOnlyList<TaskSlotDto>? Slots = null,
    string? MetadataJson = null
);
