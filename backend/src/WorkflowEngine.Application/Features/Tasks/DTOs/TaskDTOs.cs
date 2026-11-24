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
    string? CompletedBy
);
