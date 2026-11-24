namespace WorkflowEngine.Application.Features.Definitions.DTOs;

/// <summary>
/// Result returned after publishing a workflow definition version.
/// </summary>
public record WorkflowDefinitionCreatedResult(
    int DefinitionId,
    int VersionId,
    string WorkflowKey,
    int VersionNumber
);

public record WorkflowDefinitionSummaryDto(
    int DefinitionId,
    string WorkflowKey,
    string Name,
    string? Description,
    string Category,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ActiveVersionNumber,
    string? ActiveGraphJson
);
