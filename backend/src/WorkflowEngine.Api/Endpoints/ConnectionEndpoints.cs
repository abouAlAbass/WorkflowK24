using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.Commands;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Connections.Queries;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.Handlers;

namespace WorkflowEngine.Api.Endpoints;

public record CreateConnectionRequest(
    string? Id,
    string Name,
    string? BaseUrl,
    string? AuthType,
    string? SecretRef,
    List<string>? AllowedHosts,
    Dictionary<string, string>? DefaultHeaders,
    string? ServiceType = "rest",
    string? AuthMode = "static",
    string? SecretSource = "internal_encrypted",
    string? CredentialsJson = null,
    string? ExternalSecretKey = null,
    bool AllowWrite = false,
    int? MaxRows = null,
    DateTime? SecretExpiresAt = null
);

public record UpdateConnectionRequest(
    string Name,
    string? BaseUrl,
    string? AuthType,
    string? SecretRef,
    List<string>? AllowedHosts,
    Dictionary<string, string>? DefaultHeaders,
    int? ExpectedVersion = null,
    string? ServiceType = null,
    string? AuthMode = null,
    string? SecretSource = null,
    string? CredentialsJson = null,
    bool ClearCredentials = false,
    string? ExternalSecretKey = null,
    bool? AllowWrite = null,
    int? MaxRows = null
);

public record UpsertConnectionPermissionRequest(
    string SubjectType,
    string SubjectId,
    bool CanUse,
    bool CanManage
);

public record InvokeConnectionRequest(
    string Operation,
    string? TableName = null,
    string? SchemaName = null,
    string? Path = null,
    string? HttpMethod = null,
    Dictionary<string, string>? Headers = null,
    string? BodyJson = null,
    Dictionary<string, object?>? Parameters = null,
    int? TimeoutSeconds = null
);

public record UpsertUserConnectionCredentialRequest(
    string ConnectionId,
    string SecretJson
);

public record UpsertUserConnectionCredentialBodyRequest(
    string SecretJson
);

public record RotateConnectionSecretRequest(
    string? CredentialsJson = null,
    string? ExternalSecretKey = null,
    DateTime? SecretExpiresAt = null,
    int? ExpectedVersion = null
);

public record UpsertConnectionEnvironmentRequest(
    string EnvironmentName,
    string? BaseUrlOverride = null,
    string? CredentialsJsonOverride = null,
    bool ClearCredentialsOverride = false,
    string? ExternalSecretKeyOverride = null
);

public static class ConnectionEndpoints
{
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections")
            .WithTags("Connections")
            .RequireAuthorization("designs.write");

        group.MapGet("/", async (ClaimsPrincipal principal, IMessageBus bus, CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            var connections = await bus.InvokeAsync<IReadOnlyList<ConnectionDto>>(
                new GetConnectionsQuery(actor.ActorId, actor.Email, actor.Roles), ct);
            return Results.Ok(connections);
        })
        .WithName("GetConnections")
        .WithSummary("Lists external connections accessible to the caller without exposing secrets.");

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IMessageBus bus, CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var connection = await bus.InvokeAsync<ConnectionDto?>(
                    new GetConnectionByIdQuery(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return connection != null ? Results.Ok(connection) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("GetConnectionById")
        .WithSummary("Retrieves a single external connection by ID or Name without exposing secrets.");

        group.MapPost("/", async (
            CreateConnectionRequest request,
            ClaimsPrincipal principal,
            IValidator<CreateConnectionCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            var command = new CreateConnectionCommand(
                request.Id,
                request.Name,
                request.BaseUrl,
                request.AuthType ?? "none",
                request.SecretRef,
                request.AllowedHosts,
                request.DefaultHeaders,
                actor.Email ?? actor.ActorId,
                request.ServiceType ?? "rest",
                request.AuthMode ?? "static",
                request.SecretSource ?? "internal_encrypted",
                request.CredentialsJson,
                request.ExternalSecretKey,
                request.AllowWrite,
                request.MaxRows ?? WorkflowEngine.Domain.Entities.Connection.DefaultMaxRows,
                request.SecretExpiresAt);

            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var created = await bus.InvokeAsync<ConnectionDto>(command, ct);
                return Results.Created($"/api/connections/{created.Id}", created);
            }
            catch (DuplicateResourceException ex)
            {
                return Results.Json(new { code = "DUPLICATE_CONNECTION_NAME", error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateConnection")
        .WithSummary("Creates a new reusable external connection (Admin/Designer only).");

        group.MapPut("/{id}", async (
            string id,
            UpdateConnectionRequest request,
            ClaimsPrincipal principal,
            IValidator<UpdateConnectionCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            var command = new UpdateConnectionCommand(
                id,
                request.Name,
                request.BaseUrl,
                request.AuthType ?? "none",
                request.SecretRef,
                request.AllowedHosts,
                request.DefaultHeaders,
                request.ExpectedVersion,
                actor.ActorId,
                actor.Email,
                actor.Roles,
                request.ServiceType,
                request.AuthMode,
                request.SecretSource,
                request.CredentialsJson,
                request.ClearCredentials,
                request.ExternalSecretKey,
                request.AllowWrite,
                request.MaxRows);

            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var updated = await bus.InvokeAsync<ConnectionDto?>(command, ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Connection was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DuplicateResourceException ex)
            {
                return Results.Json(new { code = "DUPLICATE_CONNECTION_NAME", error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateConnection")
        .WithSummary("Updates an existing external connection with optimistic concurrency and CanManage enforcement.");

        group.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                bool deleted = await bus.InvokeAsync<bool>(
                    new DeleteConnectionCommand(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("DeleteConnection")
        .WithSummary("Deletes an external connection (Admin or CanManage only).");

        // --- Diagnostic Test Endpoint (5s timeout, no side effects, sanitized errors) ---
        group.MapPost("/{id}/test", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var result = await bus.InvokeAsync<ConnectionTestResult?>(
                    new TestConnectionCommand(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return result != null ? Results.Ok(result) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("TestConnection")
        .WithSummary("Tests connectivity for a Connection using its registered IServiceBroker (5s timeout, sanitized errors).");

        // --- Broker Invocation Endpoint (enforces CanUse) ---
        group.MapPost("/{id}/invoke", async (
            string id,
            InvokeConnectionRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var result = await bus.InvokeAsync<ServiceBrokerInvocationResult?>(
                    new InvokeConnectionCommand(
                        id,
                        request.Operation,
                        request.TableName,
                        request.SchemaName,
                        request.Path,
                        request.HttpMethod,
                        request.Headers,
                        request.BodyJson,
                        request.Parameters,
                        request.TimeoutSeconds,
                        actor.ActorId,
                        actor.Email,
                        actor.Roles), ct);
                return result != null ? Results.Ok(result) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ConnectionWriteNotAllowedException ex)
            {
                return Results.Json(new { code = ConnectionWriteNotAllowedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (UserCredentialRequiredException ex)
            {
                return Results.Json(new
                {
                    code = UserCredentialRequiredException.ErrorCode,
                    requiredConnectionId = ex.ConnectionId,
                    connectionName = ex.ConnectionName,
                    error = ex.Message
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (ExternalReauthRequiredException ex)
            {
                return Results.Json(new
                {
                    code = ExternalReauthRequiredException.ErrorCode,
                    requiredConnectionId = ex.ConnectionId,
                    connectionName = ex.ConnectionName,
                    error = ex.Message
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        })
        .WithName("InvokeConnection")
        .WithSummary("Invokes a broker operation on a Connection after verifying CanUse permission.");

        // --- Schema Discovery & Selective SmartObject Generation (Admin or Designer with CanUse) ---
        group.MapGet("/{id}/discover", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var entities = await bus.InvokeAsync<IReadOnlyList<DiscoveredEntityDto>?>(
                    new DiscoverConnectionEntitiesQuery(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return entities != null ? Results.Ok(entities) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (UserCredentialRequiredException ex)
            {
                return Results.Json(new
                {
                    code = UserCredentialRequiredException.ErrorCode,
                    requiredConnectionId = ex.ConnectionId,
                    connectionName = ex.ConnectionName,
                    error = ex.Message
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (ExternalReauthRequiredException ex)
            {
                return Results.Json(new
                {
                    code = ExternalReauthRequiredException.ErrorCode,
                    requiredConnectionId = ex.ConnectionId,
                    connectionName = ex.ConnectionName,
                    error = ex.Message
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DiscoverConnectionEntities")
        .WithSummary("Discovers remote entities and mapped SmartObject fields for a Connection (Admin or CanUse Designer).");

        group.MapPost("/{id}/generate-smartobject", async (
            string id,
            GenerateSmartObjectFromConnectionRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var created = await bus.InvokeAsync<SmartObjectDto?>(
                    new GenerateSmartObjectFromConnectionCommand(
                        ConnectionId: id,
                        EntityName: request.EntityName,
                        SmartObjectKey: request.SmartObjectKey,
                        DisplayName: request.DisplayName,
                        PrimaryKeyField: request.PrimaryKeyField,
                        SelectedFields: request.SelectedFields ?? new List<SelectedSmartObjectFieldDto>(),
                        Description: request.Description,
                        Category: request.Category,
                        PublishImmediately: request.PublishImmediately,
                        ActorId: actor.ActorId,
                        ActorEmail: actor.Email,
                        ActorRoles: actor.Roles), ct);

                return created != null
                    ? Results.Created($"/api/smartobjects/{created.Id}", created)
                    : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GenerateSmartObjectFromConnection")
        .WithSummary("Generates a SmartObject exposing only selected fields and PK-aware CRUD methods.");

        // --- Fine-Grained Connection Permissions (Admin or CanManage) ---
        group.MapGet("/{id}/permissions", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var perms = await bus.InvokeAsync<IReadOnlyList<ConnectionPermissionDto>?>(
                    new GetConnectionPermissionsQuery(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return perms != null ? Results.Ok(perms) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("GetConnectionPermissions")
        .WithSummary("Lists CanUse/CanManage governance rules for a connection (Admin or CanManage only).");

        group.MapPut("/{id}/permissions", async (
            string id,
            UpsertConnectionPermissionRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var perm = await bus.InvokeAsync<ConnectionPermissionDto?>(
                    new UpsertConnectionPermissionCommand(
                        id,
                        request.SubjectType,
                        request.SubjectId,
                        request.CanUse,
                        request.CanManage,
                        actor.ActorId,
                        actor.Email,
                        actor.Roles), ct);
                return perm != null ? Results.Ok(perm) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpsertConnectionPermission")
        .WithSummary("Grants or updates CanUse/CanManage permission for a User or Role on a connection.");

        group.MapDelete("/{id}/permissions/{permissionId}", async (
            string id,
            string permissionId,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                bool? deleted = await bus.InvokeAsync<bool?>(
                    new DeleteConnectionPermissionCommand(id, permissionId, actor.ActorId, actor.Email, actor.Roles), ct);
                if (deleted == null || !deleted.Value)
                {
                    return Results.NotFound();
                }

                return Results.NoContent();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("DeleteConnectionPermission")
        .WithSummary("Removes a permission rule from a connection.");

        // --- Secret Rotation, Operational Usage & Environment Overrides (Admin or CanManage) ---
        group.MapPost("/{id}/rotate-secret", async (
            string id,
            RotateConnectionSecretRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var updated = await bus.InvokeAsync<ConnectionDto?>(
                    new RotateConnectionSecretCommand(
                        id,
                        request.CredentialsJson,
                        request.ExternalSecretKey,
                        request.SecretExpiresAt,
                        request.ExpectedVersion,
                        actor.ActorId,
                        actor.Email,
                        actor.Roles), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { code = "CONCURRENT_MODIFICATION", error = "Connection was concurrently modified." }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RotateConnectionSecret")
        .WithSummary("Rotates encrypted credentials or external secret key, updates LastRotatedAt/SecretExpiresAt, and audits SecretRotated.");

        group.MapGet("/{id}/usage", async (
            string id,
            int? limit,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var usage = await bus.InvokeAsync<ConnectionUsageResponseDto?>(
                    new GetConnectionUsageQuery(id, limit ?? 50, actor.ActorId, actor.Email, actor.Roles), ct);
                return usage != null ? Results.Ok(usage) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("GetConnectionUsage")
        .WithSummary("Returns operational usage history and health status for a connection (Admin or CanManage only).");

        group.MapGet("/{id}/environments", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var envs = await bus.InvokeAsync<IReadOnlyList<ConnectionEnvironmentConfigDto>?>(
                    new GetConnectionEnvironmentsQuery(id, actor.ActorId, actor.Email, actor.Roles), ct);
                return envs != null ? Results.Ok(envs) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("GetConnectionEnvironments")
        .WithSummary("Lists Dev/Test/Prod environment overrides for a connection without exposing secrets.");

        group.MapPut("/{id}/environments", async (
            string id,
            UpsertConnectionEnvironmentRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var updated = await bus.InvokeAsync<ConnectionEnvironmentConfigDto?>(
                    new UpsertConnectionEnvironmentConfigCommand(
                        id,
                        request.EnvironmentName,
                        request.BaseUrlOverride,
                        request.CredentialsJsonOverride,
                        request.ClearCredentialsOverride,
                        request.ExternalSecretKeyOverride,
                        actor.ActorId,
                        actor.Email,
                        actor.Roles), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (ConnectionAccessDeniedException ex)
            {
                return Results.Json(new { code = ConnectionAccessDeniedException.ErrorCode, error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpsertConnectionEnvironment")
        .WithSummary("Creates or updates a Dev/Test/Prod environment override for a connection without exposing secrets.");

        // --- Self-Service Personal Vault (/api/me/connection-credentials) ---
        var myCredsGroup = app.MapGroup("/api/me/connection-credentials")
            .WithTags("User Connection Credentials")
            .RequireAuthorization();

        myCredsGroup.MapGet("/", async (ClaimsPrincipal principal, IMessageBus bus, CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            var creds = await bus.InvokeAsync<IReadOnlyList<UserConnectionCredentialDto>>(
                new GetMyConnectionCredentialsQuery(actor.ActorId, actor.Email), ct);
            return Results.Ok(creds);
        })
        .WithName("GetMyConnectionCredentials")
        .WithSummary("Lists personal vault status for user_sso_vault connections (never exposes secret values).");

        myCredsGroup.MapPut("/", async (
            UpsertUserConnectionCredentialRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var updated = await bus.InvokeAsync<UserConnectionCredentialDto?>(
                    new UpsertMyConnectionCredentialCommand(actor.ActorId, actor.Email, request.ConnectionId, request.SecretJson), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpsertMyConnectionCredential")
        .WithSummary("Saves or updates encrypted personal credentials for a connection in the authenticated user's vault.");

        myCredsGroup.MapPut("/{connectionId}", async (
            string connectionId,
            UpsertUserConnectionCredentialBodyRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            try
            {
                var updated = await bus.InvokeAsync<UserConnectionCredentialDto?>(
                    new UpsertMyConnectionCredentialCommand(actor.ActorId, actor.Email, connectionId, request.SecretJson), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpsertMyConnectionCredentialById")
        .WithSummary("Saves or updates encrypted personal credentials for a specific connection ID.");

        myCredsGroup.MapDelete("/{connectionId}", async (
            string connectionId,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = ExtractActor(principal);
            bool deleted = await bus.InvokeAsync<bool>(
                new DeleteMyConnectionCredentialCommand(actor.ActorId, actor.Email, connectionId), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteMyConnectionCredential")
        .WithSummary("Revokes and deletes the authenticated user's personal credentials for a connection.");

        return app;
    }

    private sealed record ActorContext(string ActorId, string? Email, IReadOnlyList<string> Roles);

    private static ActorContext ExtractActor(ClaimsPrincipal principal)
    {
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

        return new ActorContext(actorId, email, roles);
    }
}
