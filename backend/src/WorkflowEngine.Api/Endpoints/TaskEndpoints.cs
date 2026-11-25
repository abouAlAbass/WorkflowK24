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
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Api.Endpoints;

public record CompleteTaskRequest(
    string Outcome,
    string? Comments = null,
    Dictionary<string, object?>? FormPatch = null,
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
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (TaskForbiddenException ex)
            {
                return Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (FormReadOnlyException ex)
            {
                return Results.BadRequest(new { code = FormReadOnlyException.ErrorCode, error = ex.Message });
            }
            catch (SmartFormValidationFailedException ex)
            {
                return Results.ValidationProblem(
                    ex.Errors.ToDictionary(k => k.Key, v => v.Value),
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = ex.ErrorCode });
            }
            catch (InstanceSuspendedException ex)
            {
                return Results.Json(
                    new { code = InstanceSuspendedException.ErrorCode, error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Results.Json(
                    new { code = ConcurrencyConflictException.ErrorCode, error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(
                    new
                    {
                        code = ConcurrencyConflictException.ErrorCode,
                        error = $"Task '{id}' was modified concurrently by another actor. Reload and retry."
                    },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "TASK_ALREADY_COMPLETED", error = ex.Message });
            }
        })
        .WithName("CompleteTask")
        .WithSummary("Completes an assigned approval task using the JWT caller identity, applies optional editable FormPatch, and advances workflow execution.");

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

    private static TaskStatus? ParseTaskStatus(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TaskStatus>(status, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static (string CurrentUserId, string? Sub, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups) ExtractCallerIdentity(ClaimsPrincipal principal)
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
            .Concat(principal.FindAll(ClaimTypes.GroupSid))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (currentUserId, sub, roles, groups);
    }
}
