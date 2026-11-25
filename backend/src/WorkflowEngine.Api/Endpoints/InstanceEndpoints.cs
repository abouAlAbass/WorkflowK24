using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Instances.Queries;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Api.Endpoints;

public record SuspendInstanceRequest(Guid CommandId, string? Reason = null);
public record ResumeInstanceRequest(Guid CommandId);
public record CancelInstanceRequest(Guid CommandId, string Reason);
public record RetryInstanceRequest(Guid CommandId, string? Mode = null, string? FromNodeId = null);

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/instances")
            .WithTags("Instances")
            .RequireAuthorization();

        group.MapPost("/start", async (
            StartWorkflowInstanceCommand command,
            IValidator<StartWorkflowInstanceCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await bus.InvokeAsync<WorkflowInstanceStartedResult>(command, ct);
            return Results.Ok(result);
        })
        .WithName("StartWorkflowInstance")
        .WithSummary("Instantiates a new workflow execution instance for an active template.");

        group.MapGet("/", async (string? status, IMessageBus bus, CancellationToken ct) =>
        {
            WorkflowStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WorkflowStatus>(status, true, out var s))
            {
                parsedStatus = s;
            }

            var instances = await bus.InvokeAsync<IReadOnlyList<WorkflowInstanceDto>>(
                new GetWorkflowInstancesQuery(parsedStatus), ct);

            return Results.Ok(instances);
        })
        .WithName("GetWorkflowInstances")
        .WithSummary("Retrieves workflow execution instances with optional status filtering.");

        group.MapGet("/bottlenecks", async (string? nodeId, IMessageBus bus, CancellationToken ct) =>
        {
            var bottlenecks = await bus.InvokeAsync<List<ActiveBottleneckDto>>(
                new GetActiveBottlenecksQuery(nodeId), ct);

            return Results.Ok(bottlenecks);
        })
        .WithName("GetActiveBottlenecks")
        .WithSummary("Queries active workflow instances blocked at approval or decision steps.");

        group.MapGet("/{id}", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var instance = await bus.InvokeAsync<WorkflowInstanceDto?>(
                new GetWorkflowInstanceByIdQuery(id), ct);

            return instance != null ? Results.Ok(instance) : Results.NotFound();
        })
        .WithName("GetWorkflowInstanceById")
        .WithSummary("Retrieves full details of a specific workflow instance.");

        group.MapGet("/{id}/flow", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var viewFlow = await bus.InvokeAsync<ViewFlowDto?>(
                new GetInstanceViewFlowQuery(id, caller.Roles, caller.Permissions), ct);

            return viewFlow != null
                ? Results.Ok(viewFlow)
                : Results.NotFound(new { error = $"Workflow instance '{id}' not found." });
        })
        .WithName("GetInstanceViewFlow")
        .WithSummary("Retrieves the O(1) ViewFlow projection, timeline, and role-masked variables for an instance.");

        group.MapGet("/{id}/events", async (
            string id,
            int? afterSeq,
            int? limit,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            int effectiveAfterSeq = Math.Max(0, afterSeq ?? 0);
            int effectiveLimit = Math.Clamp(limit ?? 50, 1, 200);

            var page = await bus.InvokeAsync<InstanceEventsPageDto?>(
                new GetInstanceEventsQuery(id, effectiveAfterSeq, effectiveLimit, caller.Roles, caller.Permissions), ct);

            return page != null
                ? Results.Ok(page)
                : Results.NotFound(new { error = $"Workflow instance '{id}' not found." });
        })
        .WithName("GetInstanceEvents")
        .WithSummary("Retrieves cursor-paginated instance domain events ordered by sequence number.");

        // --- Operational Commands (Suspend, Resume, Cancel, Retry) ---

        group.MapPost("/{id}/commands/suspend", async (
            string id,
            SuspendInstanceRequest request,
            ClaimsPrincipal principal,
            IValidator<SuspendWorkflowInstanceCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new SuspendWorkflowInstanceCommand(id, request.CommandId, request.Reason, caller.CurrentUserId, caller.Roles);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await bus.InvokeAsync<InstanceCommandResult>(command, ct);
                return Results.Ok(result);
            }
            catch (WorkflowForbiddenException ex)
            {
                return Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidWorkflowTransitionException ex)
            {
                return Results.Json(new { code = InvalidWorkflowTransitionException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Instance was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("SuspendWorkflowInstance")
        .WithSummary("Suspends execution of an active workflow instance.");

        group.MapPost("/{id}/commands/resume", async (
            string id,
            ResumeInstanceRequest request,
            ClaimsPrincipal principal,
            IValidator<ResumeWorkflowInstanceCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new ResumeWorkflowInstanceCommand(id, request.CommandId, caller.CurrentUserId, caller.Roles);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await bus.InvokeAsync<InstanceCommandResult>(command, ct);
                return Results.Ok(result);
            }
            catch (WorkflowForbiddenException ex)
            {
                return Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidWorkflowTransitionException ex)
            {
                return Results.Json(new { code = InvalidWorkflowTransitionException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Instance was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("ResumeWorkflowInstance")
        .WithSummary("Resumes execution of a suspended workflow instance to its previous status.");

        group.MapPost("/{id}/commands/cancel", async (
            string id,
            CancelInstanceRequest request,
            ClaimsPrincipal principal,
            IValidator<CancelWorkflowInstanceCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            {
                return Results.BadRequest(new { error = "Cancellation reason must be at least 10 characters long." });
            }

            var caller = ExtractCallerIdentity(principal);
            var command = new CancelWorkflowInstanceCommand(id, request.CommandId, request.Reason, caller.CurrentUserId, caller.Roles);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await bus.InvokeAsync<InstanceCommandResult>(command, ct);
                return Results.Ok(result);
            }
            catch (WorkflowForbiddenException ex)
            {
                return Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidWorkflowTransitionException ex)
            {
                return Results.Json(new { code = InvalidWorkflowTransitionException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Instance was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("CancelWorkflowInstance")
        .WithSummary("Cancels an execution instance and revokes any open tasks.");

        group.MapPost("/{id}/commands/retry", async (
            string id,
            RetryInstanceRequest request,
            ClaimsPrincipal principal,
            IValidator<RetryWorkflowInstanceCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            var command = new RetryWorkflowInstanceCommand(id, request.CommandId, request.Mode, request.FromNodeId, caller.CurrentUserId, caller.Roles);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await bus.InvokeAsync<InstanceCommandResult>(command, ct);
                return Results.Ok(result);
            }
            catch (WorkflowForbiddenException ex)
            {
                return Results.Json(new { code = "FORBIDDEN", error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidWorkflowTransitionException ex)
            {
                return Results.Json(new { code = InvalidWorkflowTransitionException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Instance was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("RetryWorkflowInstance")
        .WithSummary("Retries a faulted workflow instance, optionally rewinding from a specific node if admin.");

        return app;
    }

    private static (string CurrentUserId, string? Sub, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups, IReadOnlyList<string> Permissions) ExtractCallerIdentity(ClaimsPrincipal principal)
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

        var permissions = principal.FindAll("permission")
            .Concat(principal.FindAll("permissions"))
            .Concat(principal.FindAll("scope"))
            .SelectMany(c => c.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (currentUserId, sub, roles, groups, permissions);
    }
}
