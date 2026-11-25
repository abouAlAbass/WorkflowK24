using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Application.Features.Instances.Queries;

public record GetActiveBottlenecksQuery(string? NodeId = null);
public record GetWorkflowInstancesQuery(WorkflowStatus? Status = null);
public record GetWorkflowInstanceByIdQuery(string InstanceId);
public record GetInstanceViewFlowQuery(
    string InstanceId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
);
public record GetInstanceEventsQuery(
    string InstanceId,
    int AfterSeq,
    int Limit,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
);
