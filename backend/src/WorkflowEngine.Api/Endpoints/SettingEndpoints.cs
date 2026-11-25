using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Settings.Commands;
using WorkflowEngine.Application.Features.Settings.DTOs;
using WorkflowEngine.Application.Features.Settings.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class SettingEndpoints
{
    public static IEndpointRouteBuilder MapSettingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization("AdminOnly");

        // 1. Get system settings
        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<SystemSettingsDto>(
                new GetSettingsQuery(), ct);

            return Results.Ok(result);
        })
        .WithName("GetSettings")
        .WithSummary("Retrieves current SMTP and database configuration settings.");

        // 2. Update system settings
        group.MapPut("/", async (
            UpdateSettingsCommand command,
            IValidator<UpdateSettingsCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var updated = await bus.InvokeAsync<SystemSettingsDto>(command, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateSettings")
        .WithSummary("Updates SMTP and database connector settings.");

        return app;
    }
}
