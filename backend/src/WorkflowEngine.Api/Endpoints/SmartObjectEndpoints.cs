using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartObjects.Commands;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.Handlers;
using WorkflowEngine.Application.Features.SmartObjects.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class SmartObjectEndpoints
{
    public static IEndpointRouteBuilder MapSmartObjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/smartobjects")
            .WithTags("SmartObjects")
            .RequireAuthorization();

        // 1. Get all Smart Object definitions
        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<SmartObjectDto>>(
                new GetSmartObjectsQuery(), ct);

            return Results.Ok(result);
        })
        .WithName("GetSmartObjects")
        .WithSummary("Retrieves all defined Smart Object entity schemas and versioned contracts.");

        // 2. Create new Smart Object definition (or new version of an existing ObjectKey)
        group.MapPost("/", async (
            CreateSmartObjectCommand command,
            IValidator<CreateSmartObjectCommand> validator,
            HttpContext httpContext,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            string caller = ResolveCallerIdentity(httpContext.User);
            try
            {
                var created = await bus.InvokeAsync<SmartObjectDto>(command with { PublishedBy = caller }, ct);
                return Results.Created($"/api/smartobjects/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "SMARTOBJECT_IMMUTABLE", message = ex.Message });
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("CreateSmartObject")
        .WithSummary("Creates a new Smart Object entity schema or versioned contract.");

        // 3. Update existing Draft Smart Object definition (Published/Archived -> 409 Conflict)
        group.MapPut("/{id}", async (
            string id,
            UpdateSmartObjectCommand command,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await bus.InvokeAsync<SmartObjectDto?>(
                    command with { Id = id }, ct);

                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "SMARTOBJECT_IMMUTABLE", message = ex.Message });
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("UpdateSmartObject")
        .WithSummary("Updates a Draft Smart Object schema metadata, fields, and methods. Published versions are immutable.");

        // 3b. Publish a Draft Smart Object version (makes it immutable & bindable by workflows)
        group.MapPost("/{id}/publish", async (
            string id,
            HttpContext httpContext,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            string caller = ResolveCallerIdentity(httpContext.User);
            try
            {
                var published = await bus.InvokeAsync<SmartObjectDto?>(
                    new PublishSmartObjectCommand(id, caller), ct);

                return published != null ? Results.Ok(published) : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "SMARTOBJECT_INVALID_STATE", message = ex.Message });
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("PublishSmartObject")
        .WithSummary("Publishes a Draft Smart Object version, freezing its contract as immutable.");

        // 3c. Archive a Smart Object version
        group.MapPost("/{id}/archive", async (
            string id,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var archived = await bus.InvokeAsync<SmartObjectDto?>(
                new ArchiveSmartObjectCommand(id), ct);

            return archived != null ? Results.Ok(archived) : Results.NotFound();
        })
        .RequireAuthorization("designs.write")
        .WithName("ArchiveSmartObject")
        .WithSummary("Archives a Smart Object version so new workflows cannot bind to it.");

        // 3d. Re-synchronize SmartObject schema against its linked Connection (diff preview or apply)
        group.MapPost("/{id}/sync-schema", async (
            string id,
            SyncSmartObjectSchemaRequest? request,
            HttpContext httpContext,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var principal = httpContext.User;
            string? email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            string actorId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? email
                ?? principal.Identity?.Name
                ?? "system";
            var roles = principal.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            try
            {
                var diff = await bus.InvokeAsync<SchemaDiffDto?>(
                    new SyncSmartObjectSchemaCommand(id, request?.Apply ?? false, actorId, email, roles), ct);
                return diff != null ? Results.Ok(diff) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("SyncSmartObjectSchema")
        .WithSummary("Compares a Connection-backed SmartObject against its remote schema and optionally applies the diff.");

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
        .RequireAuthorization("designs.write")
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

        // 5b. Paginated & filtered server-side lookup for SmartForm controls
        group.MapGet("/{id}/lookup", async (
            string id,
            string? q,
            int? page,
            int? pageSize,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<SmartObjectLookupPageDto?>(
                new LookupSmartObjectRecordsQuery(id, q, page ?? 1, pageSize ?? 25), ct);

            return result != null ? Results.Ok(result) : Results.NotFound(new { error = $"SmartObject '{id}' not found." });
        })
        .WithName("LookupSmartObjectRecords")
        .WithSummary("Searches and paginates SmartObject records server-side for Lookup form controls.");

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

    private static string ResolveCallerIdentity(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "system";
}
