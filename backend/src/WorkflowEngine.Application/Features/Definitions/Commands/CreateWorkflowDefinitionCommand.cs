namespace WorkflowEngine.Application.Features.Definitions.Commands;

/// <summary>
/// Command to create or update a workflow definition and publish a new graph version.
/// </summary>
public record CreateWorkflowDefinitionCommand(
    string WorkflowKey,
    string Name,
    string GraphJson,
    string CreatedBy,
    string? Description = null,
    string Category = "General"
);
