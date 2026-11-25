namespace WorkflowEngine.Application.Features.Instances.DTOs;

public record ViewFlowInstanceDto(
    string Id,
    string Status,
    string? SuspendedFromStatus,
    DateTime StartedAt,
    DateTime? EndedAt,
    int Version,
    int DefinitionId,
    int DefinitionVersion
);

public record ViewFlowNodeExecutionDto(
    string NodeId,
    string Type,
    string Status,
    int Attempts,
    DateTime StartedAt,
    DateTime? EndedAt,
    string? Error = null
);

public record InstanceEventDto(
    long Id,
    string InstanceId,
    int Seq,
    string Type,
    string? NodeId,
    string? ActorId,
    string ActorType,
    string? PayloadJson,
    DateTime CreatedAt
);

public record ViewFlowDto(
    ViewFlowInstanceDto Instance,
    string DefinitionJson,
    IReadOnlyList<ViewFlowNodeExecutionDto> Nodes,
    IReadOnlyList<string> ActiveNodes,
    IReadOnlyList<string> ActiveTokens,
    IReadOnlyList<InstanceEventDto> Timeline,
    Dictionary<string, object?> Variables
);

public record InstanceEventsPageDto(
    string InstanceId,
    int AfterSeq,
    int Limit,
    int? NextSeq,
    bool HasMore,
    IReadOnlyList<InstanceEventDto> Events
);
