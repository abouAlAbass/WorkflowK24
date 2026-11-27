using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Features.Tasks.Queries;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Api.Endpoints;

public record CompleteTaskRequest(
    string Outcome,
    string? Comments = null,
    Dictionary<string, object?>? FormPatch = null,
    int? ExpectedVersion = null
);

public record DelegateTaskRequest(
    string TargetAssigneeId,
    string? TargetAssigneeType = "User",
    string? Reason = null,
    int? ExpectedVersion = null
);

public record ReassignTaskRequest(
    string TargetAssigneeId,
    string? TargetAssigneeType = "User",
    string? Reason = null,
    int? ExpectedVersion = null
);

public record RecallTaskRequest(
    string? Reason = null,
    int? ExpectedVersion = null
);

public record ClaimTaskRequest(
    int? ExpectedVersion = null
);

public record ReleaseTaskRequest(
    string? Reason = null,
    int? ExpectedVersion = null
);

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapGet("/", async (
            string? status,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            TaskStatus? parsedStatus = ParseTaskStatus(status);
            var caller = ExtractCallerIdentity(principal);

            var tasks = await bus.InvokeAsync<IReadOnlyList<WorkflowTaskDto>>(
                new GetTasksQuery(caller.CurrentUserId, caller.Roles, caller.Groups, parsedStatus), ct);

            return Results.Ok(tasks);
        })
        .WithName("GetTasks")
        .WithSummary("Retrieves tasks accessible to the authenticated caller enriched with bound SmartForm metadata and instance data.");

        group.MapPost("/{id}/complete", async (
            string id,
            CompleteTaskRequest request,
            ClaimsPrincipal principal,
            IValidator<CompleteTaskCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new CompleteTaskCommand(
                TaskId: id,
                Outcome: request.Outcome,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                CurrentUserGroups: caller.Groups,
                Comments: request.Comments,
                FormPatch: request.FormPatch,
                CurrentUserSub: caller.Sub,
                ExpectedVersion: request.ExpectedVersion);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await bus.InvokeAsync<TaskCompletedResult>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("CompleteTask")
        .WithSummary("Completes an assigned approval task (or records a group Consensus/Quorum slot) using the JWT caller identity.");

        group.MapPost("/{id}/delegate", async (
            string id,
            DelegateTaskRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new DelegateTaskCommand(
                TaskId: id,
                TargetAssigneeId: request.TargetAssigneeId,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                CurrentUserGroups: caller.Groups,
                CurrentUserSub: caller.Sub,
                TargetAssigneeType: request.TargetAssigneeType,
                Reason: request.Reason,
                ExpectedVersion: request.ExpectedVersion);

            try
            {
                var result = await bus.InvokeAsync<WorkflowTaskDto>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("DelegateTask")
        .WithSummary("Delegates an active task to another authorized user without privilege elevation. Allowed even if instance is Suspended.");

        group.MapPost("/{id}/reassign", async (
            string id,
            ReassignTaskRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new ReassignTaskCommand(
                TaskId: id,
                TargetAssigneeId: request.TargetAssigneeId,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                TargetAssigneeType: request.TargetAssigneeType,
                Reason: request.Reason,
                ExpectedVersion: request.ExpectedVersion);

            try
            {
                var result = await bus.InvokeAsync<WorkflowTaskDto>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("ReassignTask")
        .WithSummary("Administratively reassigns an active task to another user, group, or role.");

        group.MapPost("/{id}/recall", async (
            string id,
            RecallTaskRequest? request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new RecallTaskCommand(
                TaskId: id,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                CurrentUserSub: caller.Sub,
                Reason: request?.Reason,
                ExpectedVersion: request?.ExpectedVersion);

            try
            {
                var result = await bus.InvokeAsync<WorkflowTaskDto>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("RecallTask")
        .WithSummary("Recalls a delegated task back to its original assignee.");

        group.MapPost("/{id}/claim", async (
            string id,
            ClaimTaskRequest? request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new ClaimTaskCommand(
                TaskId: id,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                CurrentUserGroups: caller.Groups,
                CurrentUserSub: caller.Sub,
                ExpectedVersion: request?.ExpectedVersion);

            try
            {
                var result = await bus.InvokeAsync<WorkflowTaskDto>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("ClaimTask")
        .WithSummary("Claims a group/role task with an exclusive lock for the authenticated caller.");

        group.MapPost("/{id}/release", async (
            string id,
            ReleaseTaskRequest? request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new ReleaseTaskCommand(
                TaskId: id,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                CurrentUserSub: caller.Sub,
                Reason: request?.Reason,
                ExpectedVersion: request?.ExpectedVersion);

            try
            {
                var result = await bus.InvokeAsync<WorkflowTaskDto>(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("ReleaseTask")
        .WithSummary("Releases an exclusively claimed group/role task back to the pool.");

        group.MapGet("/{id}/assignments", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            try
            {
                var assignments = await bus.InvokeAsync<IReadOnlyList<TaskAssignmentDto>>(
                    new GetTaskAssignmentsQuery(id, caller.CurrentUserId, caller.Roles, caller.Groups, caller.Sub), ct);
                return Results.Ok(assignments);
            }
            catch (Exception ex)
            {
                return MapTaskException(ex, id);
            }
        })
        .WithName("GetTaskAssignments")
        .WithSummary("Returns the append-only assignment history for a workflow task.");

        group.MapPost("/escalations/check", async (
            ITaskEscalationService escalationService,
            CancellationToken ct) =>
        {
            var result = await escalationService.CheckAndEscalateOverdueTasksAsync(ct: ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("AdminOnly")
        .WithName("CheckTaskEscalations")
        .WithSummary("Triggers an immediate SLA overdue check and escalates expired tasks.");

        group.MapGet("/assignee/{assigneeId}", async (
            string assigneeId,
            string? status,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            TaskStatus? parsedStatus = ParseTaskStatus(status);

            var tasks = await bus.InvokeAsync<IReadOnlyList<WorkflowTaskDto>>(
                new GetTasksByAssigneeQuery(assigneeId, parsedStatus), ct);

            return Results.Ok(tasks);
        })
        .WithName("GetTasksByAssignee")
        .WithSummary("Retrieves all tasks assigned to a specific user or group.");

        group.MapGet("/instance/{instanceId}", async (
            string instanceId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var tasks = await bus.InvokeAsync<IReadOnlyList<WorkflowTaskDto>>(
                new GetTasksByInstanceIdQuery(instanceId), ct);

            return Results.Ok(tasks);
        })
        .WithName("GetTasksByInstanceId")
        .WithSummary("Retrieves pending tasks for a specific workflow instance.");

        group.MapGet("/{id}", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var task = await bus.InvokeAsync<WorkflowTaskDto?>(new GetTaskByIdQuery(id), ct);
            return task != null ? Results.Ok(task) : Results.NotFound();
        })
        .WithName("GetTaskById")
        .WithSummary("Retrieves details of an individual workflow task.");

        return app;
    }

    internal static IResult MapTaskException(Exception ex, string taskId) => ex switch
    {
        KeyNotFoundException => Results.NotFound(new { error = ex.Message }),
        TaskForbiddenException => Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden),
        FormReadOnlyException => Results.BadRequest(new { code = FormReadOnlyException.ErrorCode, error = ex.Message }),
        SmartFormValidationFailedException sfv => Results.ValidationProblem(
            sfv.Errors.ToDictionary(k => k.Key, v => v.Value),
            detail: sfv.Message,
            extensions: new Dictionary<string, object?> { ["code"] = sfv.ErrorCode }),
        DelegateNotAuthorizedException => Results.Json(
            new { code = DelegateNotAuthorizedException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status422UnprocessableEntity),
        DelegationCycleException => Results.Json(
            new { code = DelegationCycleException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status422UnprocessableEntity),
        DelegationChainTooDeepException => Results.Json(
            new { code = DelegationChainTooDeepException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status422UnprocessableEntity),
        DelegationValidationException => Results.Json(
            new { code = DelegationValidationException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status422UnprocessableEntity),
        InstanceSuspendedException => Results.Json(
            new { code = InstanceSuspendedException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status409Conflict),
        TaskAlreadyClaimedException => Results.Json(
            new { code = TaskAlreadyClaimedException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status409Conflict),
        TaskClaimedByAnotherUserException => Results.Json(
            new { code = TaskClaimedByAnotherUserException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status409Conflict),
        SlotAlreadyCompletedException => Results.Json(
            new { code = SlotAlreadyCompletedException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status409Conflict),
        ConcurrencyConflictException => Results.Json(
            new { code = ConcurrencyConflictException.ErrorCode, error = ex.Message },
            statusCode: StatusCodes.Status409Conflict),
        DbUpdateConcurrencyException => Results.Json(
            new
            {
                code = ConcurrencyConflictException.ErrorCode,
                error = $"Task '{taskId}' was modified concurrently by another actor. Reload and retry."
            },
            statusCode: StatusCodes.Status409Conflict),
        InvalidOperationException => Results.Conflict(new { code = "TASK_ALREADY_COMPLETED", error = ex.Message }),
        _ => throw ex
    };

    private static TaskStatus? ParseTaskStatus(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TaskStatus>(status, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    internal static (string CurrentUserId, string? Sub, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups) ExtractCallerIdentity(ClaimsPrincipal principal)
    {
        string? email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);

        string? sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        string currentUserId = !string.IsNullOrWhiteSpace(email)
            ? email
            : (sub ?? principal.Identity?.Name ?? string.Empty);

        var roles = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("role"))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = principal.FindAll("group")
            .Concat(principal.FindAll("http://schemas.xmlsoap.org/claims/Group"))
            .Concat(principal.FindAll(ClaimTypes.GroupSid))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (currentUserId, sub, roles, groups);
    }
}
