using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Common.Interfaces;

public record ConnectionTestResult(
    bool Success,
    long LatencyMs,
    string? ErrorMessage = null
);

public record ServiceBrokerInvocationRequest(
    Connection Connection,
    string Operation,
    IReadOnlyDictionary<string, object?>? Parameters = null,
    string? TableName = null,
    string? SchemaName = null,
    string? Path = null,
    string? HttpMethod = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    string? BodyJson = null,
    string? IdempotencyKey = null,
    ResolvedExecutionSecret? Secret = null,
    int? TimeoutSeconds = null,
    string? InstanceId = null,
    string? WorkflowKey = null,
    string? NodeId = null,
    string? ActorId = null
);

public record ServiceBrokerInvocationResult(
    bool Success,
    int? StatusCode = null,
    object? Data = null,
    IReadOnlyList<Dictionary<string, object?>>? Rows = null,
    int? AffectedRows = null,
    string? ErrorCode = null,
    string? ErrorMessage = null
);

public record DiscoveredFieldDto(
    string Name,
    string SourceType,
    string MappedSmartObjectType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool SuggestedMasked
);

public record DiscoveredEntityDto(
    string EntityName,
    string DisplayName,
    string? DetectedPrimaryKey,
    IReadOnlyList<DiscoveredFieldDto> Fields,
    string? SchemaName = null,
    string? EntityType = null
);

/// <summary>
/// Extensible Service Broker contract (Nintex K2 / Power Automate connector architecture).
/// Each broker encapsulates transport, diagnostics, schema discovery, and operation execution for a specific ServiceType.
/// </summary>
public interface IServiceBroker
{
    string ServiceType { get; }

    /// <summary>
    /// Performs a read-only, side-effect-free connectivity test (strictly bounded by 5s timeout).
    /// Must never leak passwords, tokens, or connection strings in ErrorMessage.
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default);

    /// <summary>
    /// Invokes a broker-specific operation (e.g., HTTP request or parameterized CRUD).
    /// </summary>
    Task<ServiceBrokerInvocationResult> InvokeAsync(
        ServiceBrokerInvocationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Discovers remote entities (tables, views, SharePoint lists) and their mapped SmartObject fields.
    /// </summary>
    Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience overload for discovering entities without an explicit pre-resolved secret.
    /// </summary>
    Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
        Connection connection,
        CancellationToken ct = default) => DiscoverEntitiesAsync(connection, null, ct);
}

/// <summary>
/// Registry mapping Connection.ServiceType values to their corresponding IServiceBroker implementation.
/// </summary>
public interface IServiceBrokerRegistry
{
    IServiceBroker? Resolve(string serviceType);
    IReadOnlyList<string> RegisteredServiceTypes { get; }
}
