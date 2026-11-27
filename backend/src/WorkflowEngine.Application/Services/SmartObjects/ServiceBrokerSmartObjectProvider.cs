using System.Text.Json;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Services.SmartObjects;

/// <summary>
/// SmartObject provider bridging generated SmartObjects bound to Service Brokers
/// (postgres, sqlserver, sharepoint) directly to IServiceBroker.InvokeAsync.
/// </summary>
public class ServiceBrokerSmartObjectProvider : ISmartObjectProvider
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgres",
        "sqlserver",
        "sharepoint",
        "ServiceBroker"
    };

    private readonly IConnectionRepository _connectionRepository;
    private readonly IExecutionSecretResolver _secretResolver;
    private readonly IServiceBrokerRegistry _brokerRegistry;

    public ServiceBrokerSmartObjectProvider(
        IConnectionRepository connectionRepository,
        IExecutionSecretResolver secretResolver,
        IServiceBrokerRegistry brokerRegistry)
    {
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _brokerRegistry = brokerRegistry ?? throw new ArgumentNullException(nameof(brokerRegistry));
    }

    public string ProviderName => "ServiceBroker";

    public bool CanHandle(string providerName) =>
        !string.IsNullOrWhiteSpace(providerName) && SupportedProviders.Contains(providerName.Trim());

    public async Task<SmartObjectProviderResult> ExecuteAsync(
        SmartObjectProviderContext context,
        CancellationToken ct = default)
    {
        var binding = context.Method.Binding;
        string? connectionRef = binding?.ConnectionRef ?? context.SmartObject.SourceConnectionId;
        if (string.IsNullOrWhiteSpace(connectionRef))
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConfigInvalid,
                $"ServiceBroker method '{context.Method.Name}' on '{context.SmartObject.ObjectRef}' is missing 'connectionRef'.");
        }

        var connection = await _connectionRepository.GetByIdOrNameAsync(connectionRef, ct);
        if (connection == null)
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConnectionNotFound,
                $"Connection '{connectionRef}' was not found.");
        }

        var broker = _brokerRegistry.Resolve(connection.ServiceType);
        if (broker == null)
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ProviderNotFound,
                $"No IServiceBroker registered for service type '{connection.ServiceType}'.");
        }

        var secret = await _secretResolver.ResolveAsync(connection, context.Instance.InitiatorId, null, ct);
        if (secret != null)
        {
            context.SecretsToRedact.AddRange(secret.SecretsToRedact);
        }

        string? entityPath = binding?.Path ?? context.SmartObject.SourceEntityName;
        string? schemaName = null;
        string? tableName = entityPath;
        if (!string.IsNullOrWhiteSpace(entityPath) && entityPath.Contains('.'))
        {
            var parts = entityPath.Split('.', 2);
            schemaName = parts[0];
            tableName = parts[1];
        }

        string operation = context.Method.Name.Trim().ToLowerInvariant() switch
        {
            "get" => "get",
            "list" => "list",
            "create" => "create",
            "update" => "update",
            "delete" => "delete",
            _ => context.Method.Kind?.Trim().ToLowerInvariant() switch
            {
                "create" => "create",
                "update" => "update",
                "delete" => "delete",
                _ => "get"
            }
        };

        var result = await broker.InvokeAsync(new ServiceBrokerInvocationRequest(
            Connection: connection,
            Operation: operation,
            Parameters: context.Inputs,
            TableName: tableName,
            SchemaName: schemaName,
            Secret: secret,
            InstanceId: context.Instance.InstanceId,
            WorkflowKey: context.Instance.WorkflowKey,
            NodeId: context.NodeId,
            ActorId: context.Instance.InitiatorId), ct);

        if (!result.Success)
        {
            return SmartObjectProviderResult.TechnicalError(
                result.ErrorCode ?? SmartObjectErrorCodes.Unavailable,
                result.ErrorMessage ?? "ServiceBroker invocation failed.");
        }

        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (result.Data is Dictionary<string, object?> row)
        {
            foreach (var kv in row)
            {
                outputs[kv.Key] = kv.Value;
            }
        }
        else if (result.Rows is { Count: > 0 })
        {
            if (operation == "list")
            {
                outputs["items"] = result.Rows;
                outputs["count"] = result.Rows.Count;
            }
            else
            {
                foreach (var kv in result.Rows[0])
                {
                    outputs[kv.Key] = kv.Value;
                }
            }
        }
        else if (result.Data is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                outputs[prop.Name] = prop.Value.Clone();
            }
        }
        else if (operation == "delete")
        {
            outputs["deleted"] = (result.AffectedRows ?? 0) > 0;
        }

        return SmartObjectProviderResult.Success(outputs);
    }
}
