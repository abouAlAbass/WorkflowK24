namespace WorkflowEngine.Application.Features.Instances.Commands;

public record SuspendWorkflowInstanceCommand(
    string InstanceId,
    Guid CommandId,
    string? Reason,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles
);

public record ResumeWorkflowInstanceCommand(
    string InstanceId,
    Guid CommandId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles
);

public record CancelWorkflowInstanceCommand(
    string InstanceId,
    Guid CommandId,
    string Reason,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles
);

public record RetryWorkflowInstanceCommand(
    string InstanceId,
    Guid CommandId,
    string? Mode,
    string? FromNodeId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles
);
