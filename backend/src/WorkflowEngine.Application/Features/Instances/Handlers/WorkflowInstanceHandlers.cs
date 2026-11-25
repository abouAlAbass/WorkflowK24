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
        var starter = new WorkflowInstanceStarter(_definitionRepository, _instanceRepository, _executionEngine);
        var instance = await starter.StartAsync(command.WorkflowKey, command.InitiatorId, command.TriggerVariables, ct);

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
        return instances.Select(MapInstanceToDto).ToList();
    }

    public async Task<WorkflowInstanceDto?> Handle(GetWorkflowInstanceByIdQuery query, CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdAsync(query.InstanceId, ct);
        return instance != null ? MapInstanceToDto(instance) : null;
    }

    private static WorkflowInstanceDto MapInstanceToDto(WorkflowInstance i)
    {
        string workflowName = i.WorkflowVersion?.Definition?.Name ?? i.WorkflowKey;
        string? currentNodeLabel = i.CurrentNodeId;
        int totalNodes = 0;

        if (!string.IsNullOrWhiteSpace(i.WorkflowVersion?.GraphJson))
        {
            try
            {
                var graph = JsonSerializer.Deserialize<ReactFlowGraph>(i.WorkflowVersion.GraphJson);
                if (graph != null)
                {
                    totalNodes = graph.Nodes.Count;
                    var matchingNode = graph.Nodes.FirstOrDefault(n => n.Id == i.CurrentNodeId);
                    if (matchingNode != null && !string.IsNullOrWhiteSpace(matchingNode.Data.Label))
                    {
                        currentNodeLabel = matchingNode.Data.Label;
                    }
                }
            }
            catch
            {
                // Ignore malformed graph metadata during read projection
            }
        }

        int progress = i.Status switch
        {
            WorkflowStatus.Completed => 100,
            WorkflowStatus.Cancelled => 0,
            _ => CalculateRunningProgress(i, totalNodes)
        };

        DateTime endRef = i.CompletedAt ?? DateTime.UtcNow;
        double durationSeconds = Math.Max(0, (endRef - i.StartedAt).TotalSeconds);
        string department = ExtractDepartment(i.ContextVariablesJson, i.WorkflowVersion?.Definition?.Category);

        return new WorkflowInstanceDto(
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
            i.LastUpdatedAt,
            workflowName,
            currentNodeLabel,
            progress,
            durationSeconds,
            department
        );
    }

    private static int CalculateRunningProgress(WorkflowInstance instance, int totalNodes)
    {
        if (totalNodes <= 1) return 50;
        int visitedNodes = instance.AuditLogs
            .Where(l => !string.IsNullOrWhiteSpace(l.NodeId))
            .Select(l => l.NodeId)
            .Distinct()
            .Count();

        int pct = (int)Math.Round((double)Math.Max(1, visitedNodes) / totalNodes * 100);
        return Math.Clamp(pct, 10, 95);
    }

    private static string ExtractDepartment(string? variablesJson, string? fallbackCategory)
    {
        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(variablesJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "department", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        return !string.IsNullOrWhiteSpace(fallbackCategory) ? fallbackCategory : "Operations";
    }
}
