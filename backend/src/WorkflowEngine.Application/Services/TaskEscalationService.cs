using System.Text.Json;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services;

public interface ITaskEscalationService
{
    Task<EscalationCheckResult> CheckAndEscalateOverdueTasksAsync(DateTime? nowUtc = null, CancellationToken ct = default);
}

/// <summary>
/// Checks overdue workflow tasks (DueAt &lt; UtcNow), escalates them to the configured
/// node escalation target (or default fallback manager/role), appends an escalation
/// TaskAssignment entry, and writes a Task.Escalated InstanceEvent.
/// </summary>
public class TaskEscalationService : ITaskEscalationService
{
    private const string DefaultEscalationTarget = "BPM Administrator";

    private readonly IWorkflowTaskRepository _taskRepository;
    private readonly IInstanceEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TaskEscalationService(
        IWorkflowTaskRepository taskRepository,
        IInstanceEventRepository eventRepository,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<EscalationCheckResult> CheckAndEscalateOverdueTasksAsync(DateTime? nowUtc = null, CancellationToken ct = default)
    {
        DateTime utcNow = nowUtc ?? DateTime.UtcNow;
        var overdueTasks = await _taskRepository.GetOverdueTasksAsync(utcNow, ct);
        if (overdueTasks.Count == 0)
        {
            return new EscalationCheckResult(0, Array.Empty<string>());
        }

        var escalatedIds = new List<string>();

        foreach (var task in overdueTasks)
        {
            var approvalData = ResolveApprovalData(task.Instance?.WorkflowVersion?.GraphJson, task.NodeId);

            string targetId = !string.IsNullOrWhiteSpace(approvalData?.EscalationTarget)
                ? approvalData!.EscalationTarget!.Trim()
                : DefaultEscalationTarget;

            AssigneeType targetType = ParseEscalationTargetType(approvalData?.EscalationTargetType, approvalData?.EscalationTarget);
            string previousAssigneeId = task.AssigneeId;
            DateTime? dueAt = task.DueAt ?? task.DueDate;

            task.Escalate(targetId, targetType, $"SLA due date ({dueAt:O}) exceeded");

            int nextSeq = await _eventRepository.GetNextSequenceAsync(task.InstanceId, ct);
            string payloadJson = JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                nodeId = task.NodeId,
                fromAssigneeId = previousAssigneeId,
                toAssigneeId = targetId,
                toAssigneeType = targetType.ToString(),
                dueAt
            });

            var escalationEvent = new Domain.Entities.InstanceEvent(
                task.InstanceId,
                nextSeq,
                "Task.Escalated",
                task.NodeId,
                "system",
                "system",
                payloadJson,
                utcNow);

            _eventRepository.Add(escalationEvent);
            escalatedIds.Add(task.TaskId);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new EscalationCheckResult(escalatedIds.Count, escalatedIds);
    }

    private static AssigneeType ParseEscalationTargetType(string? rawType, string? targetId)
    {
        if (!string.IsNullOrWhiteSpace(rawType))
        {
            if (Enum.TryParse<AssigneeType>(rawType.Trim(), true, out var parsed))
            {
                return parsed;
            }
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            return AssigneeType.Role;
        }

        return targetId.Contains('@', StringComparison.Ordinal)
            ? AssigneeType.User
            : AssigneeType.Group;
    }

    private static ApprovalDataPayload? ResolveApprovalData(string? graphJson, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(graphJson) || string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        try
        {
            var graph = JsonSerializer.Deserialize<ReactFlowGraph>(graphJson);
            if (graph == null) return null;
            return FindApprovalDataRecursive(graph.Nodes, nodeId);
        }
        catch
        {
            return null;
        }
    }

    private static ApprovalDataPayload? FindApprovalDataRecursive(IEnumerable<ReactFlowNode> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == nodeId && node.Data.ApprovalData != null)
            {
                return node.Data.ApprovalData;
            }

            if (node.Data.StageData?.SubWorkflow?.Nodes is { Count: > 0 } subNodes)
            {
                var found = FindApprovalDataRecursive(subNodes, nodeId);
                if (found != null) return found;
            }
        }

        return null;
    }
}
