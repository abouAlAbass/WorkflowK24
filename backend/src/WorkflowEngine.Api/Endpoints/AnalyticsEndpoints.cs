using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using WorkflowEngine.Application.Features.Analytics.DTOs;
using WorkflowEngine.Application.Features.Analytics.Queries;

namespace WorkflowEngine.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics")
            .WithTags("Analytics");

        group.MapGet("/kpis", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<AnalyticsKpiDto>(
                new GetAnalyticsKpiQuery(), ct);

            return Results.Ok(result);
        })
        .WithName("GetAnalyticsKpis")
        .WithSummary("Retrieves aggregate SLA, throughput, bottlenecks, and volume by department.");

        return app;
    }
}
