namespace WorkflowEngine.Application.Features.Instances.Commands;

/// <summary>
/// Command to initiate a new runtime workflow execution instance.
/// </summary>
public record StartWorkflowInstanceCommand(
    string WorkflowKey,
    string InitiatorId,
    Dictionary<string, object>? TriggerVariables = null
);
