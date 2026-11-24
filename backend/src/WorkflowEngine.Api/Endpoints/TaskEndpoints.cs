using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Features.Tasks.Queries;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Api.Endpoints;

public record CompleteTaskRequest(string Outcome, string CompletedBy, string? Comments = null);

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks");

        group.MapPost("/{id}/complete", async (
            string id,
            CompleteTaskRequest request,
            IValidator<CompleteTaskCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var command = new CompleteTaskCommand(id, request.Outcome, request.CompletedBy, request.Comments);
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
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("CompleteTask")
        .WithSummary("Completes an assigned approval task and advances the workflow execution.");

        group.MapGet("/assignee/{assigneeId}", async (
            string assigneeId,
            string? status,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            TaskStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TaskStatus>(status, true, out var s))
            {
                parsedStatus = s;
            }

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
}
