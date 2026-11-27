using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Wolverine;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Application.Features.Users.Commands;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Application.Features.Users.Queries;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Api.Endpoints;

public record UpdateUserRequest(string Name, string Role, string Group, string Status, string? AvatarUrl = null);
public record ResetPasswordRequest(string NewPassword);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        // 1. Full user list (roles, status) - administrators only; others use /search.
        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<UserDto>>(new GetUsersQuery(), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("AdminOnly")
        .WithName("GetUsers")
        .WithSummary("Retrieves all registered system users and role assignments (administrators only).");

        // 1b. Sanitized directory search for People Picker
        group.MapGet("/search", async (
            string? q,
            int? limit,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<UserDirectoryDto>>(
                new SearchUsersQuery(q, limit ?? 20), ct);

            return Results.Ok(result);
        })
        .WithName("SearchUsers")
        .WithSummary("Searches active users for People Picker controls without exposing sensitive fields.");

        // 1c. Audit trail of user administration
        group.MapGet("/audit", async (int? limit, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<UserAuditDto>>(
                new GetUserAuditLogQuery(limit ?? 100), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Permissions.UsersManage)
        .WithName("GetUserAuditLog")
        .WithSummary("Returns the most recent user-administration audit entries.");

        // 2. Create new user
        group.MapPost("/", async (
            CreateUserCommand command,
            ClaimsPrincipal principal,
            IValidator<CreateUserCommand> validator,
            IMessageBus bus,
            IUserAuditRepository audit,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var created = await bus.InvokeAsync<UserDto>(command, ct);
            await Audit(audit, principal, UserAuditActions.Created, created, $"role={created.Role}; group={created.Group}", ct);
            return Results.Created($"/api/users/{created.Id}", created);
        })
        .RequireAuthorization(Permissions.UsersManage)
        .WithName("CreateUser")
        .WithSummary("Creates a new system user profile.");

        // 3. Update profile / role / status
        group.MapPut("/{id}", async (
            string id,
            UpdateUserRequest body,
            ClaimsPrincipal principal,
            IValidator<UpdateUserCommand> validator,
            IMessageBus bus,
            IUserAuditRepository audit,
            CancellationToken ct) =>
        {
            var command = new UpdateUserCommand(id, body.Name, body.Role, body.Group, body.Status, body.AvatarUrl);
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await bus.InvokeAsync<UserOperationResult>(command, ct);
            if (result.Status == UserOperationStatus.Ok)
            {
                await Audit(audit, principal, UserAuditActions.Updated, result.User!,
                    $"role={result.User!.Role}; group={result.User.Group}; status={result.User.Status}", ct);
            }

            return ToHttp(result);
        })
        .RequireAuthorization(Permissions.UsersManage)
        .WithName("UpdateUser")
        .WithSummary("Updates a user's profile, role and status (deactivate with status 'Inactive').");

        // 4. Reset password (also clears lockout)
        group.MapPost("/{id}/reset-password", async (
            string id,
            ResetPasswordRequest body,
            ClaimsPrincipal principal,
            IValidator<ResetUserPasswordCommand> validator,
            IMessageBus bus,
            IUserAuditRepository audit,
            CancellationToken ct) =>
        {
            var command = new ResetUserPasswordCommand(id, body.NewPassword);
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await bus.InvokeAsync<UserOperationResult>(command, ct);
            if (result.Status == UserOperationStatus.Ok)
            {
                await Audit(audit, principal, UserAuditActions.PasswordReset, result.User!, null, ct);
            }

            return result.Status == UserOperationStatus.Ok
                ? Results.Ok(new { message = "Password reset successfully." })
                : ToHttp(result);
        })
        .RequireAuthorization(Permissions.UsersManage)
        .WithName("ResetUserPassword")
        .WithSummary("Sets a new password for a user and clears any account lockout.");

        // 5. Delete (only when no pending tasks / initiated instances remain)
        group.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            IUserAuditRepository audit,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<UserOperationResult>(new DeleteUserCommand(id), ct);
            if (result.Status == UserOperationStatus.Ok)
            {
                await Audit(audit, principal, UserAuditActions.Deleted, result.User!, null, ct);
                return Results.NoContent();
            }

            return ToHttp(result);
        })
        .RequireAuthorization(Permissions.UsersManage)
        .WithName("DeleteUser")
        .WithSummary("Deletes a user with no linked work; otherwise returns 409 (deactivate instead).");

        return app;
    }

    private static IResult ToHttp(UserOperationResult result) => result.Status switch
    {
        UserOperationStatus.Ok => Results.Ok(result.User),
        UserOperationStatus.NotFound => Results.NotFound(new { error = result.Error }),
        UserOperationStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
        UserOperationStatus.Conflict => Results.Conflict(new { error = result.Error }),
        _ => Results.UnprocessableEntity(new { error = result.Error })
    };

    private static Task Audit(
        IUserAuditRepository audit, ClaimsPrincipal principal, string action, UserDto target, string? details, CancellationToken ct) =>
        audit.AddAsync(new UserAuditLog(
            CurrentActor.Id(principal), CurrentActor.Email(principal), action, target.Id, target.Email, details), ct);
}
