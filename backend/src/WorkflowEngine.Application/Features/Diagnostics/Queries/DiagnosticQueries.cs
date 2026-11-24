using WorkflowEngine.Application.Features.Diagnostics.DTOs;

namespace WorkflowEngine.Application.Features.Diagnostics.Queries;

public record GetFailingWorkflowQuery(string InstanceId);
public record GetInstanceTimelineQuery(string InstanceId);
