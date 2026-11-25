using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartForms.Validation;
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
/// Wolverine CQRS handlers for task assignments, authorization enforcement,
/// editable task form variable patching, and human-in-the-loop decisions.
/// </summary>
public class WorkflowTaskHandlers
{
    private static readonly HashSet<string> AdminRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "BPM Administrator"
    };

    private readonly IWorkflowTaskRepository _taskRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowExecutionEngine _executionEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmartFormRepository _smartFormRepository;
    private readonly ISmartFormValidationEngine _formValidationEngine;

    public WorkflowTaskHandlers(
        IWorkflowTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowExecutionEngine executionEngine,
        IUnitOfWork unitOfWork,
        ISmartFormRepository smartFormRepository,
        ISmartFormValidationEngine formValidationEngine)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _smartFormRepository = smartFormRepository ?? throw new ArgumentNullException(nameof(smartFormRepository));
        _formValidationEngine = formValidationEngine ?? throw new ArgumentNullException(nameof(formValidationEngine));
    }

    public async Task<TaskCompletedResult> Handle(CompleteTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        EnsureCallerAuthorized(task, command);

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor (expected version {command.ExpectedVersion.Value}, current version {task.Version}). Reload and retry.");
        }

        if (task.Status != TaskStatus.Pending)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be completed.");
        }

        var instance = task.Instance ?? await _instanceRepository.GetByIdAsync(task.InstanceId, ct);
        if (instance != null && instance.Status == WorkflowStatus.Suspended)
        {
            throw new InstanceSuspendedException(
                $"{InstanceSuspendedException.ErrorCode}: Workflow instance '{instance.InstanceId}' is suspended. Tasks cannot be completed while suspended.");
        }

        ReactFlowGraph? graph = DeserializeGraph(instance?.WorkflowVersion?.GraphJson);

        string completedNodeId = !string.IsNullOrWhiteSpace(task.NodeId)
            ? task.NodeId
            : instance?.CurrentNodeId ?? string.Empty;

        var approvalData = graph != null && !string.IsNullOrWhiteSpace(completedNodeId)
            ? FindApprovalDataRecursive(graph.Nodes, completedNodeId)
            : null;

        string formMode = NormalizeFormMode(approvalData?.FormMode);
        bool hasPatch = command.FormPatch is { Count: > 0 };

        if (!string.Equals(formMode, "editable", StringComparison.OrdinalIgnoreCase) && hasPatch)
        {
            throw new FormReadOnlyException(
                $"{FormReadOnlyException.ErrorCode}: Task '{command.TaskId}' form mode is '{formMode}' and does not allow form modifications.");
        }

        string? updatedVariablesJson = null;
        string? beforeDiffJson = null;
        string? afterDiffJson = null;

        if (string.Equals(formMode, "editable", StringComparison.OrdinalIgnoreCase) && hasPatch && instance != null)
        {
            var mergedVariables = ParseVariablesDictionary(instance.ContextVariablesJson);
            IReadOnlyDictionary<string, object?> effectivePatch = command.FormPatch!;

            if (!string.IsNullOrWhiteSpace(approvalData?.FormId))
            {
                var boundForm = await _smartFormRepository.GetByIdAsync(approvalData.FormId, ct);
                if (boundForm != null)
                {
                    var patchValidation = _formValidationEngine.ValidatePatch(
                        boundForm.FieldsJson,
                        command.FormPatch,
                        mergedVariables);

                    if (!patchValidation.IsValid)
                    {
                        throw new SmartFormValidationFailedException(
                            patchValidation.ErrorCode ?? SmartFormValidationFailedException.ValidationFailedCode,
                            "Task FormPatch failed server-side SmartForm validation.",
                            patchValidation.Errors);
                    }

                    effectivePatch = patchValidation.SanitizedData;
                }
            }

            var beforeDiff = new Dictionary<string, object?>(StringComparer.Ordinal);
            var afterDiff = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var kvp in effectivePatch)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    continue;
                }

                object? normalizedIncoming = NormalizeJsonValue(kvp.Value);
                mergedVariables.TryGetValue(kvp.Key, out object? existingValue);

                if (!AreJsonValuesEqual(existingValue, normalizedIncoming))
                {
                    beforeDiff[kvp.Key] = existingValue;
                    afterDiff[kvp.Key] = normalizedIncoming;
                }

                mergedVariables[kvp.Key] = normalizedIncoming;
            }

            if (beforeDiff.Count > 0)
            {
                updatedVariablesJson = JsonSerializer.Serialize(mergedVariables);
                beforeDiffJson = JsonSerializer.Serialize(beforeDiff);
                afterDiffJson = JsonSerializer.Serialize(afterDiff);
            }
        }

        task.Complete(command.Outcome, command.CurrentUserId, command.Comments);

        if (instance != null)
        {
            instance.RecordTaskDecision(
                completedNodeId,
                command.CurrentUserId,
                command.Outcome,
                updatedVariablesJson,
                beforeDiffJson,
                afterDiffJson);
        }

        string? nextNodeId = null;
        if (instance != null && graph != null)
        {
            nextNodeId = await _executionEngine.ResumeAfterTaskAsync(
                instance,
                graph,
                completedNodeId,
                command.Outcome,
                ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new TaskCompletedResult(
            task.TaskId,
            instance?.InstanceId ?? task.InstanceId,
            command.Outcome,
            nextNodeId,
            instance?.Status.ToString() ?? "COMPLETED");
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksQuery query, CancellationToken ct)
    {
        bool isAdmin = IsAdmin(query.CurrentUserRoles);
        var tasks = await _taskRepository.GetAccessibleTasksAsync(
            query.CurrentUserId,
            query.CurrentUserGroups ?? Array.Empty<string>(),
            isAdmin,
            query.Status,
            ct);

        return tasks.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksByAssigneeQuery query, CancellationToken ct)
    {
        var tasks = await _taskRepository.GetByAssigneeAsync(query.AssigneeId, query.Status, ct);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksByInstanceIdQuery query, CancellationToken ct)
    {
        var tasks = await _taskRepository.GetPendingTasksForInstanceAsync(query.InstanceId, ct);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<WorkflowTaskDto?> Handle(GetTaskByIdQuery query, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdAsync(query.TaskId, ct);
        return task != null ? MapToDto(task) : null;
    }

    private static void EnsureCallerAuthorized(Domain.Entities.WorkflowTask task, CompleteTaskCommand command)
    {
        if (IsAdmin(command.CurrentUserRoles))
        {
            return;
        }

        if (task.AssigneeType == AssigneeType.User)
        {
            bool matchesPrimaryId = !string.IsNullOrWhiteSpace(command.CurrentUserId) &&
                string.Equals(task.AssigneeId, command.CurrentUserId, StringComparison.OrdinalIgnoreCase);

            bool matchesSub = !string.IsNullOrWhiteSpace(command.CurrentUserSub) &&
                string.Equals(task.AssigneeId, command.CurrentUserSub, StringComparison.OrdinalIgnoreCase);

            if (matchesPrimaryId || matchesSub)
            {
                return;
            }
        }
        else if (task.AssigneeType == AssigneeType.Group)
        {
            bool inAssignedGroup = (command.CurrentUserGroups?.Any(g =>
                    string.Equals(g, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                (command.CurrentUserRoles?.Any(r =>
                    string.Equals(r, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ?? false);

            if (inAssignedGroup)
            {
                return;
            }
        }

        throw new TaskForbiddenException(
            $"User '{command.CurrentUserId}' is forbidden from completing task '{task.TaskId}' assigned to {task.AssigneeType} '{task.AssigneeId}'.");
    }

    private static bool IsAdmin(IReadOnlyList<string>? roles)
    {
        return roles != null && roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoleNames.Contains(r.Trim()));
    }

    private static WorkflowTaskDto MapToDto(Domain.Entities.WorkflowTask t)
    {
        string? workflowName = t.Instance?.WorkflowVersion?.Definition?.Name ?? t.Instance?.WorkflowKey;
        string? initiatorId = t.Instance?.InitiatorId;

        var graph = DeserializeGraph(t.Instance?.WorkflowVersion?.GraphJson);
        var approvalData = graph != null && !string.IsNullOrWhiteSpace(t.NodeId)
            ? FindApprovalDataRecursive(graph.Nodes, t.NodeId)
            : null;

        IReadOnlyList<ApprovalOutcomeDto>? outcomes = approvalData?.Outcomes is { Count: > 0 } list
            ? list
            : null;

        string? formId = !string.IsNullOrWhiteSpace(approvalData?.FormId)
            ? approvalData.FormId
            : null;

        string formMode = NormalizeFormMode(approvalData?.FormMode);
        Dictionary<string, object?> formData = ParseVariablesDictionary(t.Instance?.ContextVariablesJson);

        return new WorkflowTaskDto(
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
            t.CompletedBy,
            workflowName,
            initiatorId,
            outcomes,
            formId,
            formMode,
            formData,
            t.Version
        );
    }

    private static string NormalizeFormMode(string? rawMode)
    {
        return string.Equals(rawMode?.Trim(), "editable", StringComparison.OrdinalIgnoreCase)
            ? "editable"
            : "readonly";
    }

    private static ReactFlowGraph? DeserializeGraph(string? graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReactFlowGraph>(graphJson);
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
                var foundInSub = FindApprovalDataRecursive(subNodes, nodeId);
                if (foundInSub != null)
                {
                    return foundInSub;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, object?> ParseVariablesDictionary(string? json)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = ConvertJsonElement(prop.Value);
            }
        }
        catch
        {
            // Ignore malformed JSON snapshot and return empty map
        }

        return result;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
            return ConvertJsonElement(element);
        }

        return value;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out long l)
                ? l
                : element.TryGetDecimal(out decimal d)
                    ? d
                    : element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.Ordinal),
            _ => element.GetRawText()
        };
    }

    private static bool AreJsonValuesEqual(object? left, object? right)
    {
        string leftSerialized = JsonSerializer.Serialize(left);
        string rightSerialized = JsonSerializer.Serialize(right);
        return string.Equals(leftSerialized, rightSerialized, StringComparison.Ordinal);
    }
}
