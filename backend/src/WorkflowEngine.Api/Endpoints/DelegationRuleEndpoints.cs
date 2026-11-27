using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Application.Features.Tasks.Queries;

namespace WorkflowEngine.Api.Endpoints;

public record CreateDelegationRuleRequest(
    string DelegateId,
    DateTime ValidFrom,
    DateTime ValidTo,
    string? DelegatorId = null,
    string? Scope = "*",
    string? Reason = null
);

public static class DelegationRuleEndpoints
{
    public static IEndpointRouteBuilder MapDelegationRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delegation-rules")
            .WithTags("DelegationRules")
            .RequireAuthorization();

        group.MapGet("/", async (
            bool? all,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = TaskEndpoints.ExtractCallerIdentity(principal);
            var rules = await bus.InvokeAsync<IReadOnlyList<DelegationRuleDto>>(
                new GetDelegationRulesQuery(caller.CurrentUserId, caller.Roles, all ?? false), ct);
            return Results.Ok(rules);
        })
        .WithName("GetDelegationRules")
        .WithSummary("Lists Out-of-Office delegation rules for the authenticated user (or all rules for admins when all=true).");

        group.MapPost("/", async (
            CreateDelegationRuleRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = TaskEndpoints.ExtractCallerIdentity(principal);
            var command = new CreateDelegationRuleCommand(
                DelegatorId: string.IsNullOrWhiteSpace(request.DelegatorId) ? caller.CurrentUserId : request.DelegatorId,
                DelegateId: request.DelegateId,
                Scope: request.Scope,
                ValidFrom: request.ValidFrom,
                ValidTo: request.ValidTo,
                CurrentUserId: caller.CurrentUserId,
                CurrentUserRoles: caller.Roles,
                Reason: request.Reason);

            try
            {
                var created = await bus.InvokeAsync<DelegationRuleDto>(command, ct);
                return Results.Created($"/api/delegation-rules/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return TaskEndpoints.MapTaskException(ex, string.Empty);
            }
        })
        .WithName("CreateDelegationRule")
        .WithSummary("Creates a temporary Out-of-Office delegation rule with cycle and max-depth (3) validation.");

        group.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = TaskEndpoints.ExtractCallerIdentity(principal);
            try
            {
                await bus.InvokeAsync<bool>(
                    new DeleteDelegationRuleCommand(id, caller.CurrentUserId, caller.Roles), ct);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return TaskEndpoints.MapTaskException(ex, id);
            }
        })
        .WithName("DeleteDelegationRule")
        .WithSummary("Deletes an Out-of-Office delegation rule.");

        return app;
    }
}
