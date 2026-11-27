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

public record ViewFlowTokenDto(
    string Id,
    string? ParentTokenId,
    string NodeId,
    string Status,
    string? BranchId,
    string? SplitNodeId,
    int Version
);

public record ViewFlowJoinStateDto(
    string Id,
    string SplitNodeId,
    string JoinNodeId,
    int ExpectedCount,
    int ArrivedCount,
    bool JoinFired,
    int Version
);

public record ViewFlowDto(
    ViewFlowInstanceDto Instance,
    string DefinitionJson,
    IReadOnlyList<ViewFlowNodeExecutionDto> Nodes,
    IReadOnlyList<string> ActiveNodes,
    IReadOnlyList<string> ActiveTokens,
    IReadOnlyList<InstanceEventDto> Timeline,
    Dictionary<string, object?> Variables,
    IReadOnlyList<ViewFlowTokenDto>? Tokens = null,
    IReadOnlyList<ViewFlowJoinStateDto>? JoinStates = null
);

public record InstanceEventsPageDto(
    string InstanceId,
    int AfterSeq,
    int Limit,
    int? NextSeq,
    bool HasMore,
    IReadOnlyList<InstanceEventDto> Events
);
