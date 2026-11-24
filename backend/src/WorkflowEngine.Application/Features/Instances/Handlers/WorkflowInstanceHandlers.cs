using System.Text.Json;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Instances.Queries;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Instances.Handlers;

/// <summary>
/// Wolverine CQRS handlers for workflow instance lifecycle and runtime inspection.
/// </summary>
public class WorkflowInstanceHandlers
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowExecutionEngine _executionEngine;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowInstanceHandlers(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowExecutionEngine executionEngine,
        IUnitOfWork unitOfWork)
    {
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WorkflowInstanceStartedResult> Handle(StartWorkflowInstanceCommand command, CancellationToken ct)
    {
        var activeVersion = await _definitionRepository.GetActiveVersionByKeyAsync(command.WorkflowKey, ct);
        if (activeVersion == null)
        {
            throw new InvalidOperationException($"No active workflow version found for key '{command.WorkflowKey}'.");
        }

        string instanceId = $"wf-inst-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        string? variablesJson = command.TriggerVariables != null ? JsonSerializer.Serialize(command.TriggerVariables) : null;

        var instance = new WorkflowInstance(instanceId, activeVersion.VersionId, command.WorkflowKey, command.InitiatorId, variablesJson);
        _instanceRepository.Add(instance);

        // Deserialize React Flow Graph & Execute Start/Trigger Node
        var graph = JsonSerializer.Deserialize<ReactFlowGraph>(activeVersion.GraphJson);
        if (graph != null)
        {
            var triggerNode = graph.Nodes.FirstOrDefault(n => n.Data.NodeType.Equals("trigger", StringComparison.OrdinalIgnoreCase));
            if (triggerNode != null)
            {
                await _executionEngine.ExecuteNodeLogicAsync(instance, graph, triggerNode.Id, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new WorkflowInstanceStartedResult(instance.InstanceId, instance.Status.ToString(), instance.CurrentNodeId, instance.StartedAt);
    }

    public async Task<List<ActiveBottleneckDto>> Handle(GetActiveBottlenecksQuery query, CancellationToken ct)
    {
        var instances = await _instanceRepository.GetRunningInstancesAsync(query.NodeId, ct);

        return instances.Select(i =>
        {
            var pendingTask = i.Tasks.FirstOrDefault(t => t.Status == TaskStatus.Pending);
            return new ActiveBottleneckDto(
                i.InstanceId,
                i.WorkflowKey,
                i.CurrentNodeId ?? "Unknown",
                i.CurrentNodeType ?? "Unknown",
                i.InitiatorId,
                i.StartedAt,
                pendingTask?.TaskId,
                pendingTask?.TaskTitle,
                pendingTask?.AssigneeId,
                pendingTask?.AssignedAt
            );
        }).ToList();
    }

    public async Task<IReadOnlyList<WorkflowInstanceDto>> Handle(GetWorkflowInstancesQuery query, CancellationToken ct)
    {
        var instances = await _instanceRepository.GetAllAsync(query.Status, ct);

        return instances.Select(i => new WorkflowInstanceDto(
            i.InstanceId,
            i.VersionId,
            i.WorkflowKey,
            i.Status.ToString(),
            i.CurrentNodeId,
            i.CurrentNodeType,
            i.InitiatorId,
            i.ContextVariablesJson,
            i.StartedAt,
            i.CompletedAt,
            i.LastUpdatedAt
        )).ToList();
    }

    public async Task<WorkflowInstanceDto?> Handle(GetWorkflowInstanceByIdQuery query, CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdAsync(query.InstanceId, ct);
        if (instance == null) return null;

        return new WorkflowInstanceDto(
            instance.InstanceId,
            instance.VersionId,
            instance.WorkflowKey,
            instance.Status.ToString(),
            instance.CurrentNodeId,
            instance.CurrentNodeType,
            instance.InitiatorId,
            instance.ContextVariablesJson,
            instance.StartedAt,
            instance.CompletedAt,
            instance.LastUpdatedAt
        );
    }
}
