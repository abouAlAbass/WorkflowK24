namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Persists ConnectionUsageLog entries and updates Connection health metadata (LastUsedAt, HealthStatus, LastErrorSanitized)
/// in an isolated scope on every IServiceBroker invocation, without ever recording secrets.
/// </summary>
public interface IConnectionUsageRecorder
{
    Task RecordInvocationAsync(
        string connectionId,
        string? instanceId,
        string? workflowKey,
        string? nodeId,
        string? actorId,
        bool success,
        long durationMs,
        string? sanitizedError = null,
        CancellationToken ct = default);
}
