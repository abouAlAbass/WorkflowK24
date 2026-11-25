using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Diagnostics.DTOs;
using WorkflowEngine.Application.Features.Diagnostics.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diagnostics")
            .WithTags("Diagnostics")
            .RequireAuthorization();

        group.MapGet("/failing/{instanceId}", async (string instanceId, IMessageBus bus, CancellationToken ct) =>
        {
            var diagnostic = await bus.InvokeAsync<FailingWorkflowDiagnosticDto?>(
                new GetFailingWorkflowQuery(instanceId), ct);

            return diagnostic != null ? Results.Ok(diagnostic) : Results.NotFound();
        })
        .WithName("GetFailingWorkflowDiagnostic")
        .WithSummary("Retrieves diagnostic snapshot and stack trace for a failing workflow instance.");

        group.MapGet("/timeline/{instanceId}", async (string instanceId, IMessageBus bus, CancellationToken ct) =>
        {
            var timeline = await bus.InvokeAsync<IReadOnlyList<AuditLogDto>>(
                new GetInstanceTimelineQuery(instanceId), ct);

            return Results.Ok(timeline);
        })
        .WithName("GetInstanceTimeline")
        .WithSummary("Reconstructs the full chronological audit timeline of an instance.");

        return app;
    }
}
