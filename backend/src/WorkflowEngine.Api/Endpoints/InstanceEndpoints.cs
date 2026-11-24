using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Instances.Queries;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Api.Endpoints;

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/instances")
            .WithTags("Instances");

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

        return app;
    }
}
