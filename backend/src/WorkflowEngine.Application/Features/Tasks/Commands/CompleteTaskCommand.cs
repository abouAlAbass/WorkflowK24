namespace WorkflowEngine.Application.Features.Tasks.Commands;

/// <summary>
/// Command to complete a pending workflow task using the server-verified JWT actor identity
/// and optional editable task form patch.
/// </summary>
public record CompleteTaskCommand(
    string TaskId,
    string Outcome,
    string CurrentUserId,
    IReadOnlyList<string> CurrentUserRoles,
    IReadOnlyList<string>? CurrentUserGroups = null,
    string? Comments = null,
    Dictionary<string, object?>? FormPatch = null,
    string? CurrentUserSub = null,
    int? ExpectedVersion = null
);
