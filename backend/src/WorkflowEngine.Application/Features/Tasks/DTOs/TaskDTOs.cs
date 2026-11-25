using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Tasks.DTOs;

public record TaskCompletedResult(
    string TaskId,
    string InstanceId,
    string Outcome,
    string? NextNodeId,
    string InstanceStatus
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
    string? FormVersionId = null
);
