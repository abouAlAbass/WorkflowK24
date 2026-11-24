namespace WorkflowEngine.Application.Features.Tasks.Commands;

/// <summary>
/// Command to complete a pending workflow task and submit an approval/outcome decision.
/// </summary>
public record CompleteTaskCommand(
    string TaskId,
    string Outcome,
    string CompletedBy,
    string? Comments = null
);
