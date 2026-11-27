using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Application.Features.Tasks.Queries;

public record GetTasksQuery(
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    IReadOnlyList<string>? CurrentUserGroups = null,
    TaskStatus? Status = null
);

public record GetTasksByAssigneeQuery(string AssigneeId, TaskStatus? Status = null);
public record GetTasksByInstanceIdQuery(string InstanceId);
public record GetTaskByIdQuery(string TaskId);

public record GetTaskAssignmentsQuery(
    string TaskId,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    IReadOnlyList<string>? CurrentUserGroups = null,
    string? CurrentUserSub = null
);

public record GetDelegationRulesQuery(
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    bool IncludeAll = false
);
