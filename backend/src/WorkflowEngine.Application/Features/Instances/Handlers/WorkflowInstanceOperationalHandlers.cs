using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Instances.Handlers;

/// <summary>
/// Wolverine CQRS handlers for operational workflow instance commands:
/// Suspend, Resume, Cancel, and Retry with strict idempotency and audit event journaling.
/// </summary>
public class WorkflowInstanceOperationalHandlers
{
    private static readonly HashSet<string> AdminRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "BPM Administrator"
    };

    private static readonly HashSet<string> OperatorRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "BPM Administrator",
        "Workflow Designer",
        "Process Manager",
        "Workflow Developer"
    };

    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IInstanceEventRepository _eventRepository;
    private readonly ICommandLogRepository _commandLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowInstanceOperationalHandlers(
        IWorkflowInstanceRepository instanceRepository,
        IInstanceEventRepository eventRepository,
        ICommandLogRepository commandLogRepository,
        IUnitOfWork unitOfWork)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _commandLogRepository = commandLogRepository ?? throw new ArgumentNullException(nameof(commandLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<InstanceCommandResult> Handle(SuspendWorkflowInstanceCommand command, CancellationToken ct)
    {
        EnsureOperatorAuthorized(command.CurrentUserRoles);

        var existingLog = await _commandLogRepository.GetByIdAsync(command.CommandId, ct);
        if (existingLog != null)
        {
            return JsonSerializer.Deserialize<InstanceCommandResult>(existingLog.ResultJson)!;
        }

        var instanceCheck = await _instanceRepository.GetByIdAsync(command.InstanceId, ct);
        if (instanceCheck == null)
        {
            throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var existingInTx = await _commandLogRepository.GetByIdAsync(command.CommandId, txCt);
            if (existingInTx != null)
            {
                return JsonSerializer.Deserialize<InstanceCommandResult>(existingInTx.ResultJson)!;
            }

            var instance = await _instanceRepository.GetByIdWithDetailsAsync(command.InstanceId, txCt);
            if (instance == null)
            {
                throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
            }

            string reason = !string.IsNullOrWhiteSpace(command.Reason) ? command.Reason : "Suspended by user";
            instance.Suspend(reason);

            int nextSeq = await _eventRepository.GetNextSequenceAsync(instance.InstanceId, txCt);
            string payloadJson = JsonSerializer.Serialize(new { reason });
            var @event = new InstanceEvent(
                instance.InstanceId,
                nextSeq,
                "Instance.Suspended",
                instance.CurrentNodeId,
                command.CurrentUserId,
                "user",
                payloadJson);
            _eventRepository.Add(@event);

            var result = new InstanceCommandResult(
                instance.InstanceId,
                instance.Status.ToString(),
                "Suspend",
                command.CommandId,
                DateTime.UtcNow,
                $"Instance '{instance.InstanceId}' suspended successfully.");

            string resultJson = JsonSerializer.Serialize(result);
            _commandLogRepository.Add(new CommandLog(command.CommandId, instance.InstanceId, "Suspend", resultJson));

            await _unitOfWork.SaveChangesAsync(txCt);
            return result;
        }, ct);
    }

    public async Task<InstanceCommandResult> Handle(ResumeWorkflowInstanceCommand command, CancellationToken ct)
    {
        EnsureOperatorAuthorized(command.CurrentUserRoles);

        var existingLog = await _commandLogRepository.GetByIdAsync(command.CommandId, ct);
        if (existingLog != null)
        {
            return JsonSerializer.Deserialize<InstanceCommandResult>(existingLog.ResultJson)!;
        }

        var instanceCheck = await _instanceRepository.GetByIdAsync(command.InstanceId, ct);
        if (instanceCheck == null)
        {
            throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var existingInTx = await _commandLogRepository.GetByIdAsync(command.CommandId, txCt);
            if (existingInTx != null)
            {
                return JsonSerializer.Deserialize<InstanceCommandResult>(existingInTx.ResultJson)!;
            }

            var instance = await _instanceRepository.GetByIdWithDetailsAsync(command.InstanceId, txCt);
            if (instance == null)
            {
                throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
            }

            instance.Resume("Resumed by user");

            int nextSeq = await _eventRepository.GetNextSequenceAsync(instance.InstanceId, txCt);
            string payloadJson = JsonSerializer.Serialize(new { });
            var @event = new InstanceEvent(
                instance.InstanceId,
                nextSeq,
                "Instance.Resumed",
                instance.CurrentNodeId,
                command.CurrentUserId,
                "user",
                payloadJson);
            _eventRepository.Add(@event);

            var result = new InstanceCommandResult(
                instance.InstanceId,
                instance.Status.ToString(),
                "Resume",
                command.CommandId,
                DateTime.UtcNow,
                $"Instance '{instance.InstanceId}' resumed successfully.");

            string resultJson = JsonSerializer.Serialize(result);
            _commandLogRepository.Add(new CommandLog(command.CommandId, instance.InstanceId, "Resume", resultJson));

            await _unitOfWork.SaveChangesAsync(txCt);
            return result;
        }, ct);
    }

    public async Task<InstanceCommandResult> Handle(CancelWorkflowInstanceCommand command, CancellationToken ct)
    {
        EnsureOperatorAuthorized(command.CurrentUserRoles);

        var existingLog = await _commandLogRepository.GetByIdAsync(command.CommandId, ct);
        if (existingLog != null)
        {
            return JsonSerializer.Deserialize<InstanceCommandResult>(existingLog.ResultJson)!;
        }

        var instanceCheck = await _instanceRepository.GetByIdAsync(command.InstanceId, ct);
        if (instanceCheck == null)
        {
            throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var existingInTx = await _commandLogRepository.GetByIdAsync(command.CommandId, txCt);
            if (existingInTx != null)
            {
                return JsonSerializer.Deserialize<InstanceCommandResult>(existingInTx.ResultJson)!;
            }

            var instance = await _instanceRepository.GetByIdWithDetailsAsync(command.InstanceId, txCt);
            if (instance == null)
            {
                throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
            }

            instance.Cancel(command.Reason);

            int nextSeq = await _eventRepository.GetNextSequenceAsync(instance.InstanceId, txCt);
            string payloadJson = JsonSerializer.Serialize(new { reason = command.Reason });
            var @event = new InstanceEvent(
                instance.InstanceId,
                nextSeq,
                "Instance.Cancelled",
                instance.CurrentNodeId,
                command.CurrentUserId,
                "user",
                payloadJson);
            _eventRepository.Add(@event);

            var result = new InstanceCommandResult(
                instance.InstanceId,
                instance.Status.ToString(),
                "Cancel",
                command.CommandId,
                DateTime.UtcNow,
                $"Instance '{instance.InstanceId}' cancelled successfully.");

            string resultJson = JsonSerializer.Serialize(result);
            _commandLogRepository.Add(new CommandLog(command.CommandId, instance.InstanceId, "Cancel", resultJson));

            await _unitOfWork.SaveChangesAsync(txCt);
            return result;
        }, ct);
    }

    public async Task<InstanceCommandResult> Handle(RetryWorkflowInstanceCommand command, CancellationToken ct)
    {
        EnsureOperatorAuthorized(command.CurrentUserRoles);

        string effectiveMode = string.IsNullOrWhiteSpace(command.Mode) ? "retry_failed_node" : command.Mode.Trim().ToLowerInvariant();
        bool isRewind = effectiveMode == "retry_from_node";

        if (isRewind && !IsAdmin(command.CurrentUserRoles))
        {
            throw new WorkflowForbiddenException("Only administrators can perform a retry_from_node (rewind).");
        }

        var existingLog = await _commandLogRepository.GetByIdAsync(command.CommandId, ct);
        if (existingLog != null)
        {
            return JsonSerializer.Deserialize<InstanceCommandResult>(existingLog.ResultJson)!;
        }

        var instanceCheck = await _instanceRepository.GetByIdAsync(command.InstanceId, ct);
        if (instanceCheck == null)
        {
            throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var existingInTx = await _commandLogRepository.GetByIdAsync(command.CommandId, txCt);
            if (existingInTx != null)
            {
                return JsonSerializer.Deserialize<InstanceCommandResult>(existingInTx.ResultJson)!;
            }

            var instance = await _instanceRepository.GetByIdWithDetailsAsync(command.InstanceId, txCt);
            if (instance == null)
            {
                throw new KeyNotFoundException($"Workflow instance '{command.InstanceId}' not found.");
            }

            string reason = isRewind
                ? $"Rewind from node '{command.FromNodeId}'"
                : "Retry failed node";
            instance.Retry(reason, isRewind ? command.FromNodeId : null);

            int nextSeq = await _eventRepository.GetNextSequenceAsync(instance.InstanceId, txCt);
            string payloadJson = JsonSerializer.Serialize(new
            {
                mode = effectiveMode,
                fromNodeId = command.FromNodeId,
                isRewind
            });
            var @event = new InstanceEvent(
                instance.InstanceId,
                nextSeq,
                "Instance.RetryRequested",
                instance.CurrentNodeId,
                command.CurrentUserId,
                "user",
                payloadJson);
            _eventRepository.Add(@event);

            var result = new InstanceCommandResult(
                instance.InstanceId,
                instance.Status.ToString(),
                "Retry",
                command.CommandId,
                DateTime.UtcNow,
                $"Instance '{instance.InstanceId}' retry initiated successfully.");

            string resultJson = JsonSerializer.Serialize(result);
            _commandLogRepository.Add(new CommandLog(command.CommandId, instance.InstanceId, "Retry", resultJson));

            await _unitOfWork.SaveChangesAsync(txCt);
            return result;
        }, ct);
    }

    private static void EnsureOperatorAuthorized(IReadOnlyList<string>? roles)
    {
        if (roles == null || !roles.Any(r => !string.IsNullOrWhiteSpace(r) && OperatorRoleNames.Contains(r.Trim())))
        {
            throw new WorkflowForbiddenException("User lacks permission to execute operational commands on workflow instances.");
        }
    }

    private static bool IsAdmin(IReadOnlyList<string>? roles)
    {
        return roles != null && roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoleNames.Contains(r.Trim()));
    }
}
