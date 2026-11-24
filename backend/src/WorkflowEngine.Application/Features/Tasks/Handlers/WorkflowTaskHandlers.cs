using System.Text.Json;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Features.Tasks.Queries;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Tasks.Handlers;

/// <summary>
/// Wolverine CQRS handlers for task assignments and human-in-the-loop decisions.
/// </summary>
public class WorkflowTaskHandlers
{
    private readonly IWorkflowTaskRepository _taskRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowExecutionEngine _executionEngine;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowTaskHandlers(
        IWorkflowTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowExecutionEngine executionEngine,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TaskCompletedResult> Handle(CompleteTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        if (task.Status != TaskStatus.Pending)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already completed ({task.Status}).");
        }

        task.Complete(command.Outcome, command.CompletedBy, command.Comments);

        var instance = task.Instance ?? await _instanceRepository.GetByIdAsync(task.InstanceId, ct);
        string? nextNodeId = null;

        if (instance != null && instance.Version?.GraphJson != null && instance.CurrentNodeId != null)
        {
            var graph = JsonSerializer.Deserialize<ReactFlowGraph>(instance.Version.GraphJson);
            if (graph != null)
            {
                nextNodeId = _executionEngine.EvaluateNextNode(graph, instance.CurrentNodeId, command.Outcome, instance.ContextVariablesJson);
                if (nextNodeId != null)
                {
                    await _executionEngine.ExecuteNodeLogicAsync(instance, graph, nextNodeId, ct);
                }
                else
                {
                    instance.TransitionToNode("end-node", "end", command.Outcome);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new TaskCompletedResult(task.TaskId, instance?.InstanceId ?? task.InstanceId, command.Outcome, nextNodeId, instance?.Status.ToString() ?? "COMPLETED");
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksByAssigneeQuery query, CancellationToken ct)
    {
        var tasks = await _taskRepository.GetByAssigneeAsync(query.AssigneeId, query.Status, ct);

        return tasks.Select(t => new WorkflowTaskDto(
            t.TaskId,
            t.InstanceId,
            t.NodeId,
            t.TaskTitle,
            t.AssigneeType.ToString(),
            t.AssigneeId,
            t.Status.ToString(),
            t.SelectedOutcome,
            t.Comments,
            t.DueDate,
            t.AssignedAt,
            t.CompletedAt,
            t.CompletedBy
        )).ToList();
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksByInstanceIdQuery query, CancellationToken ct)
    {
        var tasks = await _taskRepository.GetPendingTasksForInstanceAsync(query.InstanceId, ct);

        return tasks.Select(t => new WorkflowTaskDto(
            t.TaskId,
            t.InstanceId,
            t.NodeId,
            t.TaskTitle,
            t.AssigneeType.ToString(),
            t.AssigneeId,
            t.Status.ToString(),
            t.SelectedOutcome,
            t.Comments,
            t.DueDate,
            t.AssignedAt,
            t.CompletedAt,
            t.CompletedBy
        )).ToList();
    }

    public async Task<WorkflowTaskDto?> Handle(GetTaskByIdQuery query, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdAsync(query.TaskId, ct);
        if (task == null) return null;

        return new WorkflowTaskDto(
            task.TaskId,
            task.InstanceId,
            task.NodeId,
            task.TaskTitle,
            task.AssigneeType.ToString(),
            task.AssigneeId,
            task.Status.ToString(),
            task.SelectedOutcome,
            task.Comments,
            task.DueDate,
            task.AssignedAt,
            task.CompletedAt,
            task.CompletedBy
        );
    }
}
