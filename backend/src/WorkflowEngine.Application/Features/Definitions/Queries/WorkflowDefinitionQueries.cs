using WorkflowEngine.Application.Features.Definitions.DTOs;

namespace WorkflowEngine.Application.Features.Definitions.Queries;

public record GetWorkflowDefinitionsQuery;
public record GetWorkflowDefinitionByKeyQuery(string WorkflowKey);
