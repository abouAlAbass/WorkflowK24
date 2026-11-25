namespace WorkflowEngine.Application.Features.Instances.DTOs;

public record InstanceCommandResult(
    string InstanceId,
    string Status,
    string CommandType,
    Guid CommandId,
    DateTime ExecutedAt,
    string? Message = null
);
