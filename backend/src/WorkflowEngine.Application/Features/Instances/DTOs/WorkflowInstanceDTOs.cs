namespace WorkflowEngine.Application.Features.Instances.DTOs;

public record WorkflowInstanceStartedResult(
    string InstanceId,
    string Status,
    string? CurrentNodeId,
    DateTime StartedAt
);

public record WorkflowInstanceDto(
    string InstanceId,
    int VersionId,
    string WorkflowKey,
    string Status,
    string? CurrentNodeId,
    string? CurrentNodeType,
    string InitiatorId,
    string? ContextVariablesJson,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime LastUpdatedAt
);

public record ActiveBottleneckDto(
    string InstanceId,
    string WorkflowKey,
    string CurrentNodeId,
    string CurrentNodeType,
    string InitiatorId,
    DateTime StartedAt,
    string? PendingTaskId,
    string? TaskTitle,
    string? AssigneeId,
    DateTime? TaskAssignedAt
);
