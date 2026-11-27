using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Features.Tasks.Queries;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Tasks.Handlers;

/// <summary>
/// Wolverine CQRS handlers for task assignments, delegation, recall, administrative reassignment,
/// group slots (Claim/Release, Consensus, Quorum), Out-of-Office delegation rules,
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
    private readonly IDelegationRuleRepository? _delegationRuleRepository;
    private readonly ISystemUserRepository? _userRepository;
    private readonly IInstanceEventRepository? _eventRepository;

    public WorkflowTaskHandlers(
        IWorkflowTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowExecutionEngine executionEngine,
        IUnitOfWork unitOfWork,
        ISmartFormRepository smartFormRepository,
        ISmartFormValidationEngine formValidationEngine,
        IDelegationRuleRepository? delegationRuleRepository = null,
        ISystemUserRepository? userRepository = null,
        IInstanceEventRepository? eventRepository = null)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _smartFormRepository = smartFormRepository ?? throw new ArgumentNullException(nameof(smartFormRepository));
        _formValidationEngine = formValidationEngine ?? throw new ArgumentNullException(nameof(formValidationEngine));
        _delegationRuleRepository = delegationRuleRepository;
        _userRepository = userRepository;
        _eventRepository = eventRepository;
    }

    public async Task<TaskCompletedResult> Handle(CompleteTaskCommand command, CancellationToken ct)
    {
        const int maxConcurrencyRetries = 12;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(
                    innerCt => ExecuteCompleteTaskAsync(command, innerCt),
                    ct);
            }
            catch (Exception ex) when (attempt < maxConcurrencyRetries && IsRetryableConcurrencyException(ex))
            {
                await Task.Delay(15 * attempt + Random.Shared.Next(10, 45), ct);
            }
        }
    }

    private static bool IsRetryableConcurrencyException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            string typeName = current.GetType().Name;
            if (string.Equals(typeName, "DbUpdateConcurrencyException", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(typeName, "DbUpdateException", StringComparison.Ordinal))
            {
                string innerMsg = current.InnerException?.Message ?? current.Message;
                if (innerMsg.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("2601", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("2627", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("40001", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("40P01", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (current.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<TaskCompletedResult> ExecuteCompleteTaskAsync(CompleteTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        EnsureCallerAuthorized(task, command.CurrentUserId, command.CurrentUserSub, command.CurrentUserRoles, command.CurrentUserGroups);

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor (expected version {command.ExpectedVersion.Value}, current version {task.Version}). Reload and retry.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be completed.");
        }

        var instance = task.Instance ?? await _instanceRepository.GetByIdAsync(task.InstanceId, ct);
        if (instance != null && instance.Status == WorkflowStatus.Suspended)
        {
            throw new InstanceSuspendedException(
                $"{InstanceSuspendedException.ErrorCode}: Workflow instance '{instance.InstanceId}' is suspended. Tasks cannot be completed while suspended.");
        }

        // Exclusive lock enforcement for Claimed tasks
        if (!string.IsNullOrWhiteSpace(task.ClaimedByUserId) &&
            !IsAdmin(command.CurrentUserRoles) &&
            !MatchesUser(task.ClaimedByUserId, command.CurrentUserId, command.CurrentUserSub))
        {
            throw new TaskClaimedByAnotherUserException(
                $"{TaskClaimedByAnotherUserException.ErrorCode}: Task '{task.TaskId}' is currently claimed by '{task.ClaimedByUserId}'.");
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
                string? targetFieldsJson = null;
                string? targetRulesJson = null;

                if (!string.IsNullOrWhiteSpace(instance.SmartFormVersionId))
                {
                    var pinnedVersion = await _smartFormRepository.GetVersionByIdAsync(
                        approvalData.FormId,
                        instance.SmartFormVersionId,
                        ct);
                    targetFieldsJson = pinnedVersion?.FieldsJson;
                    targetRulesJson = pinnedVersion?.RulesJson;
                }

                if (targetFieldsJson == null)
                {
                    var boundForm = await _smartFormRepository.GetByIdAsync(approvalData.FormId, ct);
                    var activeVer = boundForm?.GetActivePublishedVersion();
                    targetFieldsJson = activeVer?.FieldsJson ?? boundForm?.FieldsJson;
                    targetRulesJson = activeVer?.RulesJson ?? boundForm?.RulesJson;
                }

                if (targetFieldsJson != null)
                {
                    var patchValidation = _formValidationEngine.ValidatePatch(
                        targetFieldsJson,
                        command.FormPatch,
                        mergedVariables,
                        targetRulesJson);

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

        // If this task belongs to a parallel branch token, isolate FormPatch writes on the token
        // so Join merge_strategy resolves them deterministically at the Join barrier.
        string? instanceLevelUpdatedVariablesJson = updatedVariablesJson;
        if (!string.IsNullOrWhiteSpace(task.TokenId) && instance != null && afterDiffJson != null)
        {
            var branchToken = instance.Tokens.FirstOrDefault(t =>
                string.Equals(t.Id, task.TokenId, StringComparison.Ordinal));

            if (branchToken != null)
            {
                var branchVars = ParseVariablesDictionary(branchToken.BranchVariablesJson);
                var patchedVars = ParseVariablesDictionary(afterDiffJson);
                foreach (var kvp in patchedVars)
                {
                    branchVars[kvp.Key] = kvp.Value;
                }

                branchToken.UpdateBranchVariables(JsonSerializer.Serialize(branchVars));
                instanceLevelUpdatedVariablesJson = null;
            }
        }

        // Group/Role multi-slot resolution policies (Consensus / Quorum)
        if (task.ResolutionPolicy is TaskResolutionPolicy.Consensus or TaskResolutionPolicy.Quorum)
        {
            bool alreadyVoted = task.Slots.Any(s =>
                s.Status == TaskSlotStatus.Completed &&
                MatchesUser(s.AssigneeUserId, command.CurrentUserId, command.CurrentUserSub));

            if (alreadyVoted)
            {
                throw new SlotAlreadyCompletedException(
                    $"{SlotAlreadyCompletedException.ErrorCode}: User '{command.CurrentUserId}' has already submitted a decision slot for task '{task.TaskId}'.");
            }

            task.RecordSlotDecision(command.CurrentUserId, command.Outcome, command.Comments, task.OnBehalfOfUserId);

            int requiredVotes = await ResolveRequiredVotesAsync(task, ct);
            bool isReject = command.Outcome.Equals("reject", StringComparison.OrdinalIgnoreCase);

            var completedSlots = task.Slots.Where(s => s.Status == TaskSlotStatus.Completed).ToList();
            int matchingOutcomeCount = completedSlots.Count(s =>
                string.Equals(s.Outcome, command.Outcome, StringComparison.OrdinalIgnoreCase));

            bool taskResolved = task.ResolutionPolicy == TaskResolutionPolicy.Consensus
                ? (isReject || matchingOutcomeCount >= requiredVotes)
                : matchingOutcomeCount >= requiredVotes;

            if (!taskResolved)
            {
                await AppendInstanceEventAsync(
                    task.InstanceId,
                    "Task.SlotCompleted",
                    completedNodeId,
                    command.CurrentUserId,
                    "user",
                    JsonSerializer.Serialize(new
                    {
                        taskId = task.TaskId,
                        outcome = command.Outcome,
                        completedSlots = completedSlots.Count,
                        requiredVotes
                    }),
                    ct);

                await _unitOfWork.SaveChangesAsync(ct);

                return new TaskCompletedResult(
                    task.TaskId,
                    instance?.InstanceId ?? task.InstanceId,
                    command.Outcome,
                    instance?.CurrentNodeId,
                    instance?.Status.ToString() ?? "Waiting",
                    command.CurrentUserId,
                    task.OnBehalfOfUserId,
                    IsTaskFullyCompleted: false);
            }
        }

        task.Complete(command.Outcome, command.CurrentUserId, command.Comments, task.OnBehalfOfUserId);

        if (instance != null)
        {
            instance.RecordTaskDecision(
                completedNodeId,
                command.CurrentUserId,
                command.Outcome,
                instanceLevelUpdatedVariablesJson,
                beforeDiffJson,
                afterDiffJson);
        }

        string? nextNodeId = null;
        if (instance != null && graph != null)
        {
            if (IsCredentialRequiredTask(task))
            {
                if (instance.Status == WorkflowStatus.Waiting)
                {
                    instance.Continue();
                }

                await _executionEngine.ExecuteNodeLogicAsync(instance, graph, completedNodeId, ct);
                nextNodeId = instance.CurrentNodeId;
            }
            else
            {
                nextNodeId = await _executionEngine.ResumeAfterTaskAsync(
                    instance,
                    graph,
                    completedNodeId,
                    command.Outcome,
                    ct,
                    tokenId: task.TokenId);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new TaskCompletedResult(
            task.TaskId,
            instance?.InstanceId ?? task.InstanceId,
            command.Outcome,
            nextNodeId,
            instance?.Status.ToString() ?? "COMPLETED",
            task.CompletedByUserId,
            task.OnBehalfOfUserId,
            IsTaskFullyCompleted: true);
    }

    public async Task<WorkflowTaskDto> Handle(DelegateTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        // Delegation is allowed even when instance is Suspended, as long as task is active
        EnsureCallerAuthorized(task, command.CurrentUserId, command.CurrentUserSub, command.CurrentUserRoles, command.CurrentUserGroups);

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be delegated.");
        }

        if (string.IsNullOrWhiteSpace(command.TargetAssigneeId))
        {
            throw new DelegationValidationException("TargetAssigneeId is required.");
        }

        var targetType = ParseAssigneeType(command.TargetAssigneeType, AssigneeType.User);
        var instance = task.Instance ?? await _instanceRepository.GetByIdAsync(task.InstanceId, ct);
        ReactFlowGraph? graph = DeserializeGraph(instance?.WorkflowVersion?.GraphJson);
        var approvalData = graph != null && !string.IsNullOrWhiteSpace(task.NodeId)
            ? FindApprovalDataRecursive(graph.Nodes, task.NodeId)
            : null;

        if (approvalData?.AllowReassignment == false && !IsAdmin(command.CurrentUserRoles))
        {
            throw new DelegateNotAuthorizedException(
                $"{DelegateNotAuthorizedException.ErrorCode}: Reassignment/delegation is disabled on approval node '{task.NodeId}'.");
        }

        // No privilege escalation: verify target user exists, is Active, and satisfies node allowed roles if configured
        if (targetType == AssigneeType.User && _userRepository != null)
        {
            var targetUser = await ResolveUserAsync(command.TargetAssigneeId, ct);
            if (targetUser == null || !string.Equals(targetUser.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new DelegateNotAuthorizedException(
                    $"{DelegateNotAuthorizedException.ErrorCode}: Target delegate '{command.TargetAssigneeId}' does not exist or is not active.");
            }

            if (approvalData?.AllowedRoles is { Count: > 0 } allowedRoles &&
                !IsAdmin(new[] { targetUser.Role }) &&
                !allowedRoles.Any(r => string.Equals(r, targetUser.Role, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DelegateNotAuthorizedException(
                    $"{DelegateNotAuthorizedException.ErrorCode}: Target delegate '{command.TargetAssigneeId}' does not hold an authorized role for this task.");
            }
        }

        string previousAssigneeId = task.AssigneeId;
        task.Delegate(command.TargetAssigneeId.Trim(), targetType, command.CurrentUserId, command.Reason);

        await AppendInstanceEventAsync(
            task.InstanceId,
            "Task.Delegated",
            task.NodeId,
            command.CurrentUserId,
            "user",
            JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                fromAssigneeId = previousAssigneeId,
                toAssigneeId = task.AssigneeId,
                toAssigneeType = task.AssigneeType.ToString(),
                reason = command.Reason
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<WorkflowTaskDto> Handle(ReassignTaskCommand command, CancellationToken ct)
    {
        if (!IsAdmin(command.CurrentUserRoles))
        {
            throw new TaskForbiddenException("Only administrators can administratively reassign tasks.");
        }

        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be reassigned.");
        }

        if (string.IsNullOrWhiteSpace(command.TargetAssigneeId))
        {
            throw new DelegationValidationException("TargetAssigneeId is required.");
        }

        var targetType = ParseAssigneeType(command.TargetAssigneeType, AssigneeType.User);
        string previousAssigneeId = task.AssigneeId;

        task.Reassign(command.TargetAssigneeId.Trim(), targetType, command.CurrentUserId, command.Reason);

        await AppendInstanceEventAsync(
            task.InstanceId,
            "Task.Reassigned",
            task.NodeId,
            command.CurrentUserId,
            "user",
            JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                fromAssigneeId = previousAssigneeId,
                toAssigneeId = task.AssigneeId,
                toAssigneeType = task.AssigneeType.ToString(),
                reason = command.Reason
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<WorkflowTaskDto> Handle(RecallTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        bool isOriginalAssignee = MatchesUser(task.OriginalAssigneeId, command.CurrentUserId, command.CurrentUserSub);
        if (!isOriginalAssignee && !IsAdmin(command.CurrentUserRoles))
        {
            throw new TaskForbiddenException(
                $"Only the original assignee '{task.OriginalAssigneeId}' or an administrator can recall task '{task.TaskId}'.");
        }

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be recalled.");
        }

        string previousAssigneeId = task.AssigneeId;
        task.Recall(command.CurrentUserId, command.Reason);

        await AppendInstanceEventAsync(
            task.InstanceId,
            "Task.Recalled",
            task.NodeId,
            command.CurrentUserId,
            "user",
            JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                fromAssigneeId = previousAssigneeId,
                toAssigneeId = task.AssigneeId,
                reason = command.Reason
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<WorkflowTaskDto> Handle(ClaimTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        EnsureCallerAuthorized(task, command.CurrentUserId, command.CurrentUserSub, command.CurrentUserRoles, command.CurrentUserGroups);

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be claimed.");
        }

        if (!string.IsNullOrWhiteSpace(task.ClaimedByUserId))
        {
            if (MatchesUser(task.ClaimedByUserId, command.CurrentUserId, command.CurrentUserSub))
            {
                return MapToDto(task);
            }

            throw new TaskAlreadyClaimedException(
                $"{TaskAlreadyClaimedException.ErrorCode}: Task '{task.TaskId}' is already claimed by '{task.ClaimedByUserId}'.");
        }

        task.Claim(command.CurrentUserId);

        await AppendInstanceEventAsync(
            task.InstanceId,
            "Task.Claimed",
            task.NodeId,
            command.CurrentUserId,
            "user",
            JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                claimedByUserId = task.ClaimedByUserId
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<WorkflowTaskDto> Handle(ReleaseTaskCommand command, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdWithInstanceAsync(command.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{command.TaskId}' not found.");
        }

        if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != task.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: Task '{command.TaskId}' was modified by another actor.");
        }

        if (!task.IsActive)
        {
            throw new InvalidOperationException($"Task '{command.TaskId}' is already in terminal state '{task.Status}' and cannot be released.");
        }

        if (string.IsNullOrWhiteSpace(task.ClaimedByUserId))
        {
            return MapToDto(task);
        }

        bool isClaimant = MatchesUser(task.ClaimedByUserId, command.CurrentUserId, command.CurrentUserSub);
        if (!isClaimant && !IsAdmin(command.CurrentUserRoles))
        {
            throw new TaskForbiddenException(
                $"Only the user who claimed task '{task.TaskId}' ('{task.ClaimedByUserId}') or an administrator can release it.");
        }

        string previousClaimant = task.ClaimedByUserId;
        task.Release(command.CurrentUserId, command.Reason);

        await AppendInstanceEventAsync(
            task.InstanceId,
            "Task.Released",
            task.NodeId,
            command.CurrentUserId,
            "user",
            JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                releasedByUserId = command.CurrentUserId,
                previousClaimant
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetTaskAssignmentsQuery query, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdAsync(query.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Workflow task '{query.TaskId}' not found.");
        }

        // Allow current assignee, original assignee, group/role member, or admin to inspect history
        bool isOriginalAssignee = MatchesUser(task.OriginalAssigneeId, query.CurrentUserId, query.CurrentUserSub);
        if (!isOriginalAssignee)
        {
            EnsureCallerAuthorized(task, query.CurrentUserId, query.CurrentUserSub, query.CurrentUserRoles, query.CurrentUserGroups);
        }

        var assignments = await _taskRepository.GetAssignmentsAsync(query.TaskId, ct);
        return assignments
            .Select(a => new TaskAssignmentDto(
                a.Id,
                a.TaskId,
                a.Action,
                a.FromAssigneeId,
                a.ToAssigneeId,
                a.ToAssigneeType.ToString(),
                a.ActorUserId,
                a.Reason,
                a.CreatedAt))
            .ToList();
    }

    public async Task<DelegationRuleDto> Handle(CreateDelegationRuleCommand command, CancellationToken ct)
    {
        if (_delegationRuleRepository == null)
        {
            throw new InvalidOperationException("DelegationRuleRepository is not configured.");
        }

        bool isAdmin = IsAdmin(command.CurrentUserRoles);
        string effectiveDelegator = !string.IsNullOrWhiteSpace(command.DelegatorId)
            ? command.DelegatorId.Trim()
            : command.CurrentUserId;

        if (!isAdmin && !string.Equals(effectiveDelegator, command.CurrentUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new TaskForbiddenException("You can only create Out-of-Office delegation rules for yourself.");
        }

        if (_userRepository != null)
        {
            var delegateUser = await ResolveUserAsync(command.DelegateId, ct);
            if (delegateUser == null || !string.Equals(delegateUser.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new DelegateNotAuthorizedException(
                    $"{DelegateNotAuthorizedException.ErrorCode}: Target delegate '{command.DelegateId}' does not exist or is not active.");
            }
        }

        var rule = new DelegationRule(
            $"del-{Guid.NewGuid():N}",
            effectiveDelegator,
            command.DelegateId.Trim(),
            command.Scope,
            command.ValidFrom,
            command.ValidTo,
            command.CurrentUserId,
            command.Reason,
            active: true);

        var existingActiveRules = await _delegationRuleRepository.GetActiveRulesAsync(ct);
        DelegationResolverService.ValidateNewRule(rule, existingActiveRules);

        _delegationRuleRepository.Add(rule);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapRuleToDto(rule);
    }

    public async Task<IReadOnlyList<DelegationRuleDto>> Handle(GetDelegationRulesQuery query, CancellationToken ct)
    {
        if (_delegationRuleRepository == null)
        {
            return Array.Empty<DelegationRuleDto>();
        }

        bool isAdmin = IsAdmin(query.CurrentUserRoles);
        var rules = isAdmin && query.IncludeAll
            ? await _delegationRuleRepository.GetAllAsync(ct)
            : await _delegationRuleRepository.GetByDelegatorAsync(query.CurrentUserId, ct);

        return rules.Select(MapRuleToDto).ToList();
    }

    public async Task<bool> Handle(DeleteDelegationRuleCommand command, CancellationToken ct)
    {
        if (_delegationRuleRepository == null)
        {
            return false;
        }

        var rule = await _delegationRuleRepository.GetByIdAsync(command.RuleId, ct);
        if (rule == null)
        {
            throw new KeyNotFoundException($"Delegation rule '{command.RuleId}' not found.");
        }

        bool isAdmin = IsAdmin(command.CurrentUserRoles);
        if (!isAdmin && !string.Equals(rule.DelegatorId, command.CurrentUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new TaskForbiddenException("You can only delete your own delegation rules.");
        }

        _delegationRuleRepository.Remove(rule);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> Handle(GetTasksQuery query, CancellationToken ct)
    {
        bool isAdmin = IsAdmin(query.CurrentUserRoles);
        var tasks = await _taskRepository.GetAccessibleTasksAsync(
            query.CurrentUserId,
            query.CurrentUserGroups ?? Array.Empty<string>(),
            isAdmin,
            query.Status,
            ct,
            query.CurrentUserRoles);

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

    private async Task<int> ResolveRequiredVotesAsync(WorkflowTask task, CancellationToken ct)
    {
        if (task.QuorumCount.HasValue && task.QuorumCount.Value > 0)
        {
            return task.QuorumCount.Value;
        }

        if (task.ResolutionPolicy == TaskResolutionPolicy.Consensus && _userRepository != null)
        {
            var allUsers = await _userRepository.GetAllAsync(ct);
            int groupMembers = allUsers.Count(u =>
                string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                (task.AssigneeType == AssigneeType.Group
                    ? string.Equals(u.Group, task.AssigneeId, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(u.Role, task.AssigneeId, StringComparison.OrdinalIgnoreCase)));

            if (groupMembers > 0)
            {
                return groupMembers;
            }
        }

        return 1;
    }

    private async Task<SystemUser?> ResolveUserAsync(string userIdOrEmail, CancellationToken ct)
    {
        if (_userRepository == null || string.IsNullOrWhiteSpace(userIdOrEmail))
        {
            return null;
        }

        string trimmed = userIdOrEmail.Trim();
        var byEmail = await _userRepository.GetByEmailAsync(trimmed, ct);
        if (byEmail != null)
        {
            return byEmail;
        }

        return await _userRepository.GetByIdAsync(trimmed, ct);
    }

    private async Task AppendInstanceEventAsync(
        string instanceId,
        string type,
        string? nodeId,
        string? actorId,
        string actorType,
        string? payloadJson,
        CancellationToken ct)
    {
        if (_eventRepository == null)
        {
            return;
        }

        int nextSeq = await _eventRepository.GetNextSequenceAsync(instanceId, ct);
        _eventRepository.Add(new InstanceEvent(instanceId, nextSeq, type, nodeId, actorId, actorType, payloadJson));
    }

    private static void EnsureCallerAuthorized(
        WorkflowTask task,
        string currentUserId,
        string? currentUserSub,
        IReadOnlyList<string>? currentUserRoles,
        IReadOnlyList<string>? currentUserGroups)
    {
        if (IsAdmin(currentUserRoles))
        {
            return;
        }

        if (task.AssigneeType == AssigneeType.User)
        {
            if (MatchesUser(task.AssigneeId, currentUserId, currentUserSub))
            {
                return;
            }
        }
        else if (task.AssigneeType == AssigneeType.Group)
        {
            bool inAssignedGroup = (currentUserGroups?.Any(g =>
                    string.Equals(g, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                (currentUserRoles?.Any(r =>
                    string.Equals(r, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ?? false);

            if (inAssignedGroup)
            {
                return;
            }
        }
        else if (task.AssigneeType == AssigneeType.Role)
        {
            bool hasAssignedRole = currentUserRoles?.Any(r =>
                string.Equals(r, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ?? false;

            if (hasAssignedRole)
            {
                return;
            }
        }

        throw new TaskForbiddenException(
            $"User '{currentUserId}' is forbidden from acting on task '{task.TaskId}' assigned to {task.AssigneeType} '{task.AssigneeId}'.");
    }

    private static bool MatchesUser(string? targetId, string? currentUserId, string? currentUserSub)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return false;
        }

        bool matchesPrimary = !string.IsNullOrWhiteSpace(currentUserId) &&
            string.Equals(targetId, currentUserId, StringComparison.OrdinalIgnoreCase);

        bool matchesSub = !string.IsNullOrWhiteSpace(currentUserSub) &&
            string.Equals(targetId, currentUserSub, StringComparison.OrdinalIgnoreCase);

        return matchesPrimary || matchesSub;
    }

    private static AssigneeType ParseAssigneeType(string? raw, AssigneeType defaultValue)
    {
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<AssigneeType>(raw.Trim(), true, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static bool IsAdmin(IReadOnlyList<string>? roles)
    {
        return roles != null && roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoleNames.Contains(r.Trim()));
    }

    private static DelegationRuleDto MapRuleToDto(DelegationRule r) => new(
        r.Id,
        r.DelegatorId,
        r.DelegateId,
        r.Scope,
        r.ValidFrom,
        r.ValidTo,
        r.Active,
        r.Reason,
        r.CreatedBy,
        r.CreatedAt);

    private static WorkflowTaskDto MapToDto(WorkflowTask t)
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
        string? formVersionId = t.Instance?.SmartFormVersionId;

        IReadOnlyList<TaskSlotDto>? slots = t.Slots.Count > 0
            ? t.Slots.Select(s => new TaskSlotDto(
                s.Id,
                s.TaskId,
                s.AssigneeUserId,
                s.Status.ToString(),
                s.Outcome,
                s.Comments,
                s.ClaimedAt,
                s.CompletedAt,
                s.CompletedByUserId,
                s.OnBehalfOfUserId)).ToList()
            : null;

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
            t.Version,
            formVersionId,
            t.OriginalAssigneeId,
            t.ResolutionPolicy.ToString(),
            t.QuorumCount,
            t.ClaimedByUserId,
            t.ClaimedAt,
            t.DueAt ?? t.DueDate,
            t.CompletedByUserId ?? t.CompletedBy,
            t.OnBehalfOfUserId,
            t.EscalatedAt,
            slots,
            t.MetadataJson
        );
    }

    private static bool IsCredentialRequiredTask(WorkflowTask task)
    {
        if (string.IsNullOrWhiteSpace(task.MetadataJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(task.MetadataJson);
            return doc.RootElement.TryGetProperty("type", out var t) &&
                   string.Equals(t.GetString(), "user_credential_required", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
