using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Records ConnectionUsageLog entries and updates Connection operational health in an isolated DI scope
/// so every IServiceBroker invocation is audited cleanly regardless of ambient transactions.
/// Never stores secret values.
/// </summary>
public class ConnectionUsageRecorder : IConnectionUsageRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectionUsageRecorder> _logger;

    public ConnectionUsageRecorder(
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectionUsageRecorder> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordInvocationAsync(
        string connectionId,
        string? instanceId,
        string? workflowKey,
        string? nodeId,
        string? actorId,
        bool success,
        long durationMs,
        string? sanitizedError = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var connectionRepo = scope.ServiceProvider.GetRequiredService<IConnectionRepository>();
            var usageRepo = scope.ServiceProvider.GetRequiredService<IConnectionUsageLogRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var connection = await connectionRepo.GetByIdAsync(connectionId, ct);
            if (connection == null)
                return;

            var log = new ConnectionUsageLog(
                connectionId: connection.Id,
                instanceId: instanceId,
                workflowKey: workflowKey,
                nodeId: nodeId,
                actorId: actorId,
                success: success,
                durationMs: durationMs);

            usageRepo.Add(log);
            connection.RecordUsage(success, durationMs, sanitizedError);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record connection usage log for connection {ConnectionId}.", connectionId);
        }
    }
}
