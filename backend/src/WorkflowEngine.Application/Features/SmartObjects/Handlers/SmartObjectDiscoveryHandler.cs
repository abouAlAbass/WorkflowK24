using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.SmartObjects.Commands;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.SmartObjects;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Handlers;

public record DiscoverConnectionEntitiesQuery(
    string ConnectionId,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record GenerateSmartObjectFromConnectionCommand(
    string ConnectionId,
    string EntityName,
    string SmartObjectKey,
    string? DisplayName,
    string? PrimaryKeyField,
    List<SelectedSmartObjectFieldDto> SelectedFields,
    string? Description,
    string? Category,
    bool PublishImmediately,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record SyncSmartObjectSchemaCommand(
    string SmartObjectId,
    bool Apply,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

/// <summary>
/// Wolverine CQRS handler for Connection schema discovery, selective SmartObject generation
/// (with PK-aware CRUD method generation), and remote schema diff/synchronization.
/// </summary>
public class SmartObjectDiscoveryHandler
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly ISmartObjectRepository _smartObjectRepository;
    private readonly IConnectionAccessService _accessService;
    private readonly IExecutionSecretResolver _secretResolver;
    private readonly IServiceBrokerRegistry _brokerRegistry;
    private readonly IAuditService _auditService;

    public SmartObjectDiscoveryHandler(
        IConnectionRepository connectionRepository,
        ISmartObjectRepository smartObjectRepository,
        IConnectionAccessService accessService,
        IExecutionSecretResolver secretResolver,
        IServiceBrokerRegistry brokerRegistry,
        IAuditService auditService)
    {
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _smartObjectRepository = smartObjectRepository ?? throw new ArgumentNullException(nameof(smartObjectRepository));
        _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _brokerRegistry = brokerRegistry ?? throw new ArgumentNullException(nameof(brokerRegistry));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<IReadOnlyList<DiscoveredEntityDto>?> Handle(
        DiscoverConnectionEntitiesQuery query,
        CancellationToken ct)
    {
        var connection = await _connectionRepository.GetByIdOrNameAsync(query.ConnectionId, ct);
        if (connection == null)
        {
            return null;
        }

        bool canUse = await _accessService.CanUseAsync(connection, query.ActorId, query.ActorEmail, query.ActorRoles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                connection.Id,
                $"Actor '{query.ActorId}' does not have CanUse permission to discover entities on connection '{connection.Name}'.");
        }

        var broker = _brokerRegistry.Resolve(connection.ServiceType)
            ?? throw new InvalidOperationException($"No service broker registered for service type '{connection.ServiceType}'.");

        var alternates = string.IsNullOrWhiteSpace(query.ActorEmail) ? null : new[] { query.ActorEmail };
        var secret = await _secretResolver.ResolveAsync(connection, query.ActorId, alternates, ct);

        return await broker.DiscoverEntitiesAsync(connection, secret, ct);
    }

    public async Task<SmartObjectDto?> Handle(
        GenerateSmartObjectFromConnectionCommand command,
        CancellationToken ct)
    {
        var connection = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        if (connection == null)
        {
            return null;
        }

        bool canUse = await _accessService.CanUseAsync(connection, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                connection.Id,
                $"Actor '{command.ActorId}' does not have CanUse permission on connection '{connection.Name}'.");
        }

        if (string.IsNullOrWhiteSpace(command.EntityName))
        {
            throw new ArgumentException("EntityName is required.");
        }

        if (string.IsNullOrWhiteSpace(command.SmartObjectKey))
        {
            throw new ArgumentException("SmartObjectKey is required.");
        }

        if (command.SelectedFields == null || command.SelectedFields.Count == 0)
        {
            throw new ArgumentException("At least one field must be selected in 'selectedFields'.");
        }

        string? normalizedPk = string.IsNullOrWhiteSpace(command.PrimaryKeyField)
            ? null
            : command.PrimaryKeyField.Trim();

        // Ensure selected primaryKeyField actually exists in selectedFields if provided
        if (normalizedPk != null &&
            !command.SelectedFields.Any(f => string.Equals(f.Name?.Trim(), normalizedPk, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Primary key field '{normalizedPk}' must be included in selectedFields.");
        }

        var fields = command.SelectedFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f =>
            {
                string fieldName = f.Name.Trim();
                bool isPk = normalizedPk != null && string.Equals(fieldName, normalizedPk, StringComparison.OrdinalIgnoreCase);
                string mappedType = SmartObjectTypeMapper.MapToSmartObjectType(f.Type);
                string displayName = !string.IsNullOrWhiteSpace(f.DisplayName)
                    ? f.DisplayName.Trim()
                    : SmartObjectTypeMapper.FormatDisplayName(fieldName);

                return new SmartObjectFieldDto(
                    Name: fieldName,
                    Type: mappedType,
                    IsPrimaryKey: isPk,
                    IsRequired: isPk || f.IsRequired,
                    DisplayName: displayName,
                    Masked: f.Masked);
            })
            .ToList();

        if (fields.Count == 0)
        {
            throw new ArgumentException("At least one valid field is required.");
        }

        string objectKey = command.SmartObjectKey.Trim();
        string displayNameFinal = !string.IsNullOrWhiteSpace(command.DisplayName)
            ? command.DisplayName.Trim()
            : SmartObjectTypeMapper.FormatDisplayName(objectKey);
        string description = !string.IsNullOrWhiteSpace(command.Description)
            ? command.Description.Trim()
            : $"Generated from {connection.ServiceType} connection '{connection.Name}' ({command.EntityName.Trim()}).";
        string category = !string.IsNullOrWhiteSpace(command.Category)
            ? command.Category.Trim()
            : connection.ServiceType.ToUpperInvariant();

        var methods = BuildGeneratedMethods(connection, command.EntityName.Trim(), fields, normalizedPk);

        int latestVersion = await _smartObjectRepository.GetLatestVersionNumberAsync(objectKey, ct);
        int version = latestVersion + 1;
        var status = command.PublishImmediately ? SmartObjectStatus.Published : SmartObjectStatus.Draft;

        var entity = new SmartObjectEntity(
            id: $"so-{Guid.NewGuid():N}",
            name: objectKey,
            displayName: displayNameFinal,
            description: description,
            category: category,
            fieldsJson: JsonSerializer.Serialize(fields),
            recordsCount: 0,
            version: version,
            status: status,
            methodsJson: JsonSerializer.Serialize(methods),
            publishedBy: status == SmartObjectStatus.Published ? (command.ActorEmail ?? command.ActorId) : null,
            sourceConnectionId: connection.Id,
            sourceEntityName: command.EntityName.Trim(),
            primaryKeyField: normalizedPk);

        await _smartObjectRepository.AddAsync(entity, ct);

        await _auditService.RecordAsync(
            SecurityAuditEventType.SmartObjectGenerated,
            command.ActorEmail ?? command.ActorId,
            "SmartObject",
            entity.Id,
            new Dictionary<string, object?>
            {
                ["smartObjectKey"] = entity.Name,
                ["connectionId"] = connection.Id,
                ["entityName"] = command.EntityName.Trim(),
                ["selectedFieldCount"] = fields.Count,
                ["maskedFieldCount"] = fields.Count(f => f.Masked),
                ["primaryKeyField"] = normalizedPk,
                ["generatedMethods"] = methods.Select(m => m.Name).ToList()
            },
            ct);

        return CreateSmartObjectHandler.MapToDto(entity, fields, methods);
    }

    public async Task<SchemaDiffDto?> Handle(
        SyncSmartObjectSchemaCommand command,
        CancellationToken ct)
    {
        var smartObject = await _smartObjectRepository.GetByIdAsync(command.SmartObjectId, ct);
        if (smartObject == null)
        {
            return null;
        }

        string? connectionId = smartObject.SourceConnectionId;
        string? entityName = smartObject.SourceEntityName;

        // Fallback to method bindings if entity was linked via method binding
        var existingMethods = DeserializeList<SmartObjectMethodDto>(smartObject.MethodsJson);
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            connectionId = existingMethods.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Binding?.ConnectionRef))?.Binding?.ConnectionRef;
        }
        if (string.IsNullOrWhiteSpace(entityName))
        {
            entityName = existingMethods.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Binding?.Path))?.Binding?.Path;
        }

        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(entityName))
        {
            throw new InvalidOperationException($"SmartObject '{smartObject.Name}' is not linked to an external Connection and source entity.");
        }

        var connection = await _connectionRepository.GetByIdOrNameAsync(connectionId, ct)
            ?? throw new InvalidOperationException($"Source connection '{connectionId}' no longer exists.");

        bool canUse = await _accessService.CanUseAsync(connection, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                connection.Id,
                $"Actor '{command.ActorId}' does not have CanUse permission on connection '{connection.Name}'.");
        }

        var broker = _brokerRegistry.Resolve(connection.ServiceType)
            ?? throw new InvalidOperationException($"No service broker registered for service type '{connection.ServiceType}'.");

        var alternates = string.IsNullOrWhiteSpace(command.ActorEmail) ? null : new[] { command.ActorEmail };
        var secret = await _secretResolver.ResolveAsync(connection, command.ActorId, alternates, ct);

        var discoveredEntities = await broker.DiscoverEntitiesAsync(connection, secret, ct);
        var remoteEntity = discoveredEntities.FirstOrDefault(e =>
            string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"{e.SchemaName}.{e.EntityName}", entityName, StringComparison.OrdinalIgnoreCase));

        if (remoteEntity == null)
        {
            throw new KeyNotFoundException($"Source entity '{entityName}' was not found on connection '{connection.Name}'.");
        }

        var currentFields = DeserializeList<SmartObjectFieldDto>(smartObject.FieldsJson);
        var currentByName = currentFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        var remoteByName = remoteEntity.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        var addedFields = remoteEntity.Fields
            .Where(rf => !currentByName.ContainsKey(rf.Name))
            .ToList();

        var removedFields = currentFields
            .Where(cf => !remoteByName.ContainsKey(cf.Name))
            .ToList();

        var typeChangedFields = new List<SchemaFieldTypeChangeDto>();
        foreach (var rf in remoteEntity.Fields)
        {
            if (currentByName.TryGetValue(rf.Name, out var cf))
            {
                string currentNormalized = SmartObjectTypeMapper.MapToSmartObjectType(cf.Type);
                string remoteNormalized = SmartObjectTypeMapper.MapToSmartObjectType(rf.MappedSmartObjectType);
                if (!string.Equals(currentNormalized, remoteNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    typeChangedFields.Add(new SchemaFieldTypeChangeDto(
                        Name: rf.Name,
                        PreviousType: currentNormalized,
                        NewType: remoteNormalized,
                        SourceType: rf.SourceType));
                }
            }
        }

        string? currentPk = !string.IsNullOrWhiteSpace(smartObject.PrimaryKeyField)
            ? smartObject.PrimaryKeyField
            : currentFields.FirstOrDefault(f => f.IsPrimaryKey == true)?.Name;
        string? remotePk = remoteEntity.DetectedPrimaryKey;
        bool primaryKeyChanged = !string.Equals(currentPk, remotePk, StringComparison.OrdinalIgnoreCase);

        bool hasChanges = addedFields.Count > 0
            || removedFields.Count > 0
            || typeChangedFields.Count > 0
            || primaryKeyChanged;

        bool applied = false;
        if (command.Apply)
        {
            var updatedFields = new List<SmartObjectFieldDto>();

            // Retain existing fields that still exist remotely, updating type & PK while preserving displayName & masked
            foreach (var cf in currentFields)
            {
                if (!remoteByName.TryGetValue(cf.Name, out var rf))
                {
                    continue; // Removed remotely
                }

                bool isPk = remotePk != null && string.Equals(cf.Name, remotePk, StringComparison.OrdinalIgnoreCase);
                updatedFields.Add(cf with
                {
                    Type = rf.MappedSmartObjectType,
                    IsPrimaryKey = isPk,
                    IsRequired = isPk || !rf.IsNullable
                });
            }

            // Append newly added remote fields
            foreach (var rf in addedFields)
            {
                bool isPk = remotePk != null && string.Equals(rf.Name, remotePk, StringComparison.OrdinalIgnoreCase);
                updatedFields.Add(new SmartObjectFieldDto(
                    Name: rf.Name,
                    Type: rf.MappedSmartObjectType,
                    IsPrimaryKey: isPk,
                    IsRequired: isPk || !rf.IsNullable,
                    DisplayName: SmartObjectTypeMapper.FormatDisplayName(rf.Name),
                    Masked: rf.SuggestedMasked));
            }

            var updatedMethods = BuildGeneratedMethods(connection, entityName, updatedFields, remotePk);

            smartObject.ApplySchemaSync(
                JsonSerializer.Serialize(updatedFields),
                JsonSerializer.Serialize(updatedMethods),
                remotePk);

            await _smartObjectRepository.UpdateAsync(smartObject, ct);
            applied = true;

            await _auditService.RecordAsync(
                SecurityAuditEventType.SmartObjectSchemaSynced,
                command.ActorEmail ?? command.ActorId,
                "SmartObject",
                smartObject.Id,
                new Dictionary<string, object?>
                {
                    ["smartObjectKey"] = smartObject.Name,
                    ["connectionId"] = connection.Id,
                    ["entityName"] = entityName,
                    ["addedCount"] = addedFields.Count,
                    ["removedCount"] = removedFields.Count,
                    ["typeChangedCount"] = typeChangedFields.Count,
                    ["primaryKeyChanged"] = primaryKeyChanged
                },
                ct);
        }

        return new SchemaDiffDto(
            SmartObjectId: smartObject.Id,
            ConnectionId: connection.Id,
            EntityName: entityName,
            AddedFields: addedFields,
            RemovedFields: removedFields,
            TypeChangedFields: typeChangedFields,
            PrimaryKeyChanged: primaryKeyChanged,
            PreviousPrimaryKey: currentPk,
            NewPrimaryKey: remotePk,
            HasChanges: hasChanges,
            Applied: applied);
    }

    internal static List<SmartObjectMethodDto> BuildGeneratedMethods(
        Connection connection,
        string entityName,
        List<SmartObjectFieldDto> fields,
        string? primaryKeyField)
    {
        var methods = new List<SmartObjectMethodDto>();
        string providerName = connection.ServiceType;
        bool hasPrimaryKey = !string.IsNullOrWhiteSpace(primaryKeyField);

        var allOutputs = fields
            .Select(f => new SmartObjectMethodParameterDto(
                Name: f.Name,
                Type: f.Type,
                Required: f.IsRequired ?? false,
                IsSensitive: false)) // Masked fields are redacted dynamically by ViewFlow/InstanceEvents unless Admin
            .ToList();

        var pkInputList = hasPrimaryKey
            ? new List<SmartObjectMethodParameterDto>
            {
                new(
                    Name: primaryKeyField!,
                    Type: fields.FirstOrDefault(f => string.Equals(f.Name, primaryKeyField, StringComparison.OrdinalIgnoreCase))?.Type ?? "string",
                    Required: true)
            }
            : new List<SmartObjectMethodParameterDto>();

        var baseBinding = new SmartObjectMethodBindingDto(
            Provider: providerName,
            ConnectionRef: connection.Id,
            Path: entityName,
            RecordKeyInput: primaryKeyField);

        // 1. Get (always generated)
        methods.Add(new SmartObjectMethodDto(
            Name: "Get",
            Kind: "read",
            Inputs: pkInputList,
            Outputs: allOutputs,
            Idempotent: true,
            Binding: baseBinding));

        // 2. List (always generated)
        methods.Add(new SmartObjectMethodDto(
            Name: "List",
            Kind: "read",
            Inputs: new List<SmartObjectMethodParameterDto>
            {
                new("limit", "number", Required: false),
                new("offset", "number", Required: false)
            },
            Outputs: allOutputs,
            Idempotent: true,
            Binding: baseBinding));

        // 3. Create (only when connection.AllowWrite == true)
        if (connection.AllowWrite)
        {
            var createInputs = fields
                .Select(f => new SmartObjectMethodParameterDto(
                    Name: f.Name,
                    Type: f.Type,
                    Required: f.IsRequired ?? false))
                .ToList();

            methods.Add(new SmartObjectMethodDto(
                Name: "Create",
                Kind: "create",
                Inputs: createInputs,
                Outputs: allOutputs,
                Idempotent: false,
                Binding: baseBinding));
        }

        // 4. Update & Delete (ONLY when connection.AllowWrite == true AND primaryKeyField is specified)
        if (connection.AllowWrite && hasPrimaryKey)
        {
            var updateInputs = fields
                .Select(f => new SmartObjectMethodParameterDto(
                    Name: f.Name,
                    Type: f.Type,
                    Required: string.Equals(f.Name, primaryKeyField, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            methods.Add(new SmartObjectMethodDto(
                Name: "Update",
                Kind: "update",
                Inputs: updateInputs,
                Outputs: allOutputs,
                Idempotent: true,
                Binding: baseBinding));

            methods.Add(new SmartObjectMethodDto(
                Name: "Delete",
                Kind: "delete",
                Inputs: pkInputList,
                Outputs: new List<SmartObjectMethodParameterDto>
                {
                    new("deleted", "boolean", Required: false)
                },
                Idempotent: true,
                Binding: baseBinding));
        }

        return methods;
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }
}
