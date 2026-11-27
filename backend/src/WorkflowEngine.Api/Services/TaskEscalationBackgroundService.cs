using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Api.Services;

/// <summary>
/// Periodic background worker checking for overdue workflow tasks (DueAt &lt; UtcNow)
/// and escalating them via ITaskEscalationService.
/// </summary>
public sealed class TaskEscalationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskEscalationBackgroundService> _logger;

    public TaskEscalationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TaskEscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool enabled = _configuration.GetValue("SlaEscalation:Enabled", true);
        if (!enabled)
        {
            return;
        }

        int intervalSeconds = Math.Max(15, _configuration.GetValue("SlaEscalation:IntervalSeconds", 60));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                using var scope = _scopeFactory.CreateScope();
                var escalationService = scope.ServiceProvider.GetRequiredService<ITaskEscalationService>();
                var result = await escalationService.CheckAndEscalateOverdueTasksAsync(ct: stoppingToken);
                if (result.EscalatedCount > 0)
                {
                    _logger.LogInformation("SLA escalation worker escalated {Count} overdue task(s).", result.EscalatedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SLA escalation background tick encountered an error.");
            }
        }
    }
}
