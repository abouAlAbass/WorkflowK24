using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Users.Commands;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Application.Features.Users.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        // 1. Get all system users
        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<UserDto>>(
                new GetUsersQuery(), ct);

            return Results.Ok(result);
        })
        .WithName("GetUsers")
        .WithSummary("Retrieves all registered system BPM users and role assignments.");

        // 2. Create new user
        group.MapPost("/", async (
            CreateUserCommand command,
            IValidator<CreateUserCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var created = await bus.InvokeAsync<UserDto>(command, ct);
            return Results.Created($"/api/users/{created.Id}", created);
        })
        .WithName("CreateUser")
        .WithSummary("Creates a new system user profile.");

        // 3. Delete user
        group.MapDelete("/{id}", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var deleted = await bus.InvokeAsync<bool>(new DeleteUserCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteUser")
        .WithSummary("Deletes a system user profile by ID.");

        return app;
    }
}
