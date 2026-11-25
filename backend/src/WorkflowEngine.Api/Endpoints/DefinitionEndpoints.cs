using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Definitions.Commands;
using WorkflowEngine.Application.Features.Definitions.DTOs;
using WorkflowEngine.Application.Features.Definitions.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class DefinitionEndpoints
{
    public static IEndpointRouteBuilder MapDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/definitions")
            .WithTags("Definitions")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateWorkflowDefinitionCommand command,
            IValidator<CreateWorkflowDefinitionCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await bus.InvokeAsync<WorkflowDefinitionCreatedResult>(command, ct);
            return Results.Created($"/api/definitions/{result.WorkflowKey}", result);
        })
        .RequireAuthorization("DesignerOrAdmin")
        .WithName("CreateWorkflowDefinition")
        .WithSummary("Saves a workflow definition and publishes a new graph version.");

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var definitions = await bus.InvokeAsync<IReadOnlyList<WorkflowDefinitionSummaryDto>>(
                new GetWorkflowDefinitionsQuery(), ct);

            return Results.Ok(definitions);
        })
        .WithName("GetWorkflowDefinitions")
        .WithSummary("Returns all registered workflow definitions with their active graph versions.");

        group.MapGet("/{key}", async (string key, IMessageBus bus, CancellationToken ct) =>
        {
            var definition = await bus.InvokeAsync<WorkflowDefinitionSummaryDto?>(
                new GetWorkflowDefinitionByKeyQuery(key), ct);

            return definition != null ? Results.Ok(definition) : Results.NotFound();
        })
        .WithName("GetWorkflowDefinitionByKey")
        .WithSummary("Retrieves a workflow definition and active graph by its unique key.");

        group.MapDelete("/{key}", async (string key, IMessageBus bus, CancellationToken ct) =>
        {
            var deleted = await bus.InvokeAsync<bool>(
                new DeleteWorkflowDefinitionCommand(key), ct);

            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("DesignerOrAdmin")
        .WithName("DeleteWorkflowDefinition")
        .WithSummary("Deletes a workflow definition, its versions, and associated instances.");

        return app;
    }
}
