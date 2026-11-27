namespace WorkflowEngine.Application.Features.Tasks.Commands;

public record DelegateTaskCommand(
    string TaskId,
    string TargetAssigneeId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    IReadOnlyList<string>? CurrentUserGroups = null,
    string? CurrentUserSub = null,
    string? TargetAssigneeType = null,
    string? Reason = null,
    int? ExpectedVersion = null
);

public record ReassignTaskCommand(
    string TaskId,
    string TargetAssigneeId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    string? TargetAssigneeType = null,
    string? Reason = null,
    int? ExpectedVersion = null
);

public record RecallTaskCommand(
    string TaskId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    string? CurrentUserSub = null,
    string? Reason = null,
    int? ExpectedVersion = null
);

public record ClaimTaskCommand(
    string TaskId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    IReadOnlyList<string>? CurrentUserGroups = null,
    string? CurrentUserSub = null,
    int? ExpectedVersion = null
);

public record ReleaseTaskCommand(
    string TaskId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    string? CurrentUserSub = null,
    string? Reason = null,
    int? ExpectedVersion = null
);

public record CreateDelegationRuleCommand(
    string DelegatorId,
    string DelegateId,
    string? Scope,
    DateTime ValidFrom,
    DateTime ValidTo,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    string? Reason = null
);

public record DeleteDelegationRuleCommand(
    string RuleId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles
);
