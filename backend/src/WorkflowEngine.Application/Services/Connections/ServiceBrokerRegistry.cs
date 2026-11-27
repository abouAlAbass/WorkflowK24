using System.Diagnostics;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services.Connections;

public class ServiceBrokerRegistry : IServiceBrokerRegistry
{
    private readonly Dictionary<string, IServiceBroker> _brokers;
    private readonly IConnectionUsageRecorder? _usageRecorder;

    public ServiceBrokerRegistry(
        IEnumerable<IServiceBroker> brokers,
        IConnectionUsageRecorder? usageRecorder = null)
    {
        _usageRecorder = usageRecorder;
        _brokers = new Dictionary<string, IServiceBroker>(StringComparer.OrdinalIgnoreCase);
        foreach (var broker in brokers ?? Array.Empty<IServiceBroker>())
        {
            if (!string.IsNullOrWhiteSpace(broker.ServiceType))
            {
                _brokers[broker.ServiceType.Trim().ToLowerInvariant()] = broker;
            }
        }
    }

    public IServiceBroker? Resolve(string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            return null;
        }

        if (!_brokers.TryGetValue(serviceType.Trim().ToLowerInvariant(), out var broker))
        {
            return null;
        }

        return _usageRecorder != null
            ? new UsageLoggingServiceBrokerDecorator(broker, _usageRecorder)
            : broker;
    }

    public IReadOnlyList<string> RegisteredServiceTypes => _brokers.Keys.OrderBy(k => k).ToList();

    private sealed class UsageLoggingServiceBrokerDecorator : IServiceBroker
    {
        private readonly IServiceBroker _inner;
        private readonly IConnectionUsageRecorder _recorder;

        public UsageLoggingServiceBrokerDecorator(IServiceBroker inner, IConnectionUsageRecorder recorder)
        {
            _inner = inner;
            _recorder = recorder;
        }

        public string ServiceType => _inner.ServiceType;

        public Task<ConnectionTestResult> TestConnectionAsync(
            Connection connection,
            ResolvedExecutionSecret? secret,
            CancellationToken ct = default) =>
            _inner.TestConnectionAsync(connection, secret, ct);

        public Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
            Connection connection,
            ResolvedExecutionSecret? secret,
            CancellationToken ct = default) =>
            _inner.DiscoverEntitiesAsync(connection, secret, ct);

        public async Task<ServiceBrokerInvocationResult> InvokeAsync(
            ServiceBrokerInvocationRequest request,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var secretsToRedact = request.Secret?.SecretsToRedact ?? Array.Empty<string>();
            try
            {
                var result = await _inner.InvokeAsync(request, ct);
                sw.Stop();

                await _recorder.RecordInvocationAsync(
                    connectionId: request.Connection.Id,
                    instanceId: request.InstanceId,
                    workflowKey: request.WorkflowKey,
                    nodeId: request.NodeId,
                    actorId: request.ActorId,
                    success: result.Success,
                    durationMs: sw.ElapsedMilliseconds,
                    sanitizedError: result.Success ? null : ErrorSanitizer.Sanitize(result.ErrorMessage, secretsToRedact),
                    ct: ct);

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _recorder.RecordInvocationAsync(
                    connectionId: request.Connection.Id,
                    instanceId: request.InstanceId,
                    workflowKey: request.WorkflowKey,
                    nodeId: request.NodeId,
                    actorId: request.ActorId,
                    success: false,
                    durationMs: sw.ElapsedMilliseconds,
                    sanitizedError: ErrorSanitizer.Sanitize(ex.Message, secretsToRedact),
                    ct: ct);
                throw;
            }
        }
    }
}
