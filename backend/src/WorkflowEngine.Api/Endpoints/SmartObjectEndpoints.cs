using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.SmartObjects.Commands;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class SmartObjectEndpoints
{
    public static IEndpointRouteBuilder MapSmartObjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/smartobjects")
            .WithTags("SmartObjects");

        // 1. Get all Smart Object definitions
        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<SmartObjectDto>>(
                new GetSmartObjectsQuery(), ct);

            return Results.Ok(result);
        })
        .WithName("GetSmartObjects")
        .WithSummary("Retrieves all defined Smart Object entity schemas.");

        // 2. Create new Smart Object definition
        group.MapPost("/", async (
            CreateSmartObjectCommand command,
            IValidator<CreateSmartObjectCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var created = await bus.InvokeAsync<SmartObjectDto>(command, ct);
            return Results.Created($"/api/smartobjects/{created.Id}", created);
        })
        .WithName("CreateSmartObject")
        .WithSummary("Creates a new Smart Object entity schema.");

        // 3. Update existing Smart Object definition
        group.MapPut("/{id}", async (
            string id,
            UpdateSmartObjectCommand command,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var updated = await bus.InvokeAsync<SmartObjectDto?>(
                command with { Id = id }, ct);

            return updated != null ? Results.Ok(updated) : Results.NotFound();
        })
        .WithName("UpdateSmartObject")
        .WithSummary("Updates an existing Smart Object schema metadata and fields.");

        // 4. Delete Smart Object definition
        group.MapDelete("/{id}", async (
            string id,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var deleted = await bus.InvokeAsync<bool>(
                new DeleteSmartObjectCommand(id), ct);

            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSmartObject")
        .WithSummary("Deletes a Smart Object schema and all its associated data records.");

        // 5. Get data records for a Smart Object
        group.MapGet("/{smartObjectId}/records", async (
            string smartObjectId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var records = await bus.InvokeAsync<IReadOnlyList<Dictionary<string, object?>>>(
                new GetSmartObjectRecordsQuery(smartObjectId), ct);

            return Results.Ok(records);
        })
        .WithName("GetSmartObjectRecords")
        .WithSummary("Retrieves all data rows for a specified Smart Object.");

        // 6. Add new data record for a Smart Object
        group.MapPost("/{smartObjectId}/records", async (
            string smartObjectId,
            Dictionary<string, object?> record,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var added = await bus.InvokeAsync<Dictionary<string, object?>>(
                new AddSmartObjectRecordCommand(smartObjectId, record), ct);

            return Results.Created($"/api/smartobjects/{smartObjectId}/records", added);
        })
        .WithName("AddSmartObjectRecord")
        .WithSummary("Inserts a new data record into a Smart Object.");

        // 7. Update data record
        group.MapPut("/{smartObjectId}/records/{primaryKeyVal}", async (
            string smartObjectId,
            string primaryKeyVal,
            Dictionary<string, object?> record,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var updated = await bus.InvokeAsync<Dictionary<string, object?>?>(
                new UpdateSmartObjectRecordCommand(smartObjectId, primaryKeyVal, record), ct);

            return Results.Ok(updated);
        })
        .WithName("UpdateSmartObjectRecord")
        .WithSummary("Updates an existing data row identified by its primary key.");

        // 8. Delete data record
        group.MapDelete("/{smartObjectId}/records/{primaryKeyVal}", async (
            string smartObjectId,
            string primaryKeyVal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var deleted = await bus.InvokeAsync<bool>(
                new DeleteSmartObjectRecordCommand(smartObjectId, primaryKeyVal), ct);

            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSmartObjectRecord")
        .WithSummary("Deletes a data row from a Smart Object.");

        return app;
    }
}
