using System.Text.Json.Serialization;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.SmartObjects.DTOs;

public record SmartObjectFieldDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("isPrimaryKey")] bool? IsPrimaryKey = false,
    [property: JsonPropertyName("isRequired")] bool? IsRequired = false,
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("masked")] bool Masked = false
);

public record SmartObjectMethodParameterDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type = "string",
    [property: JsonPropertyName("required")] bool Required = false,
    [property: JsonPropertyName("isSensitive")] bool IsSensitive = false
);

public record SmartObjectMethodBindingDto(
    [property: JsonPropertyName("provider")] string Provider = "InternalStore",
    [property: JsonPropertyName("connectionRef")] string? ConnectionRef = null,
    [property: JsonPropertyName("httpMethod")] string? HttpMethod = null,
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers = null,
    [property: JsonPropertyName("queryParams")] Dictionary<string, string>? QueryParams = null,
    [property: JsonPropertyName("bodyTemplate")] System.Text.Json.JsonElement? BodyTemplate = null,
    [property: JsonPropertyName("responseMapping")] Dictionary<string, string>? ResponseMapping = null,
    [property: JsonPropertyName("recordKeyInput")] string? RecordKeyInput = null,
    [property: JsonPropertyName("timeoutMs")] int? TimeoutMs = null,
    [property: JsonPropertyName("maxResponseBytes")] int? MaxResponseBytes = null
);

public record SmartObjectMethodDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind = "read",
    [property: JsonPropertyName("inputs")] List<SmartObjectMethodParameterDto>? Inputs = null,
    [property: JsonPropertyName("outputs")] List<SmartObjectMethodParameterDto>? Outputs = null,
    [property: JsonPropertyName("idempotent")] bool Idempotent = true,
    [property: JsonPropertyName("binding")] SmartObjectMethodBindingDto? Binding = null
);

public record SmartObjectDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("recordsCount")] int RecordsCount,
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto> Fields,
    [property: JsonPropertyName("version")] int Version = 1,
    [property: JsonPropertyName("status")] string Status = "Draft",
    [property: JsonPropertyName("objectRef")] string? ObjectRef = null,
    [property: JsonPropertyName("methods")] List<SmartObjectMethodDto>? Methods = null,
    [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt = null,
    [property: JsonPropertyName("publishedBy")] string? PublishedBy = null,
    [property: JsonPropertyName("sourceConnectionId")] string? SourceConnectionId = null,
    [property: JsonPropertyName("sourceEntityName")] string? SourceEntityName = null,
    [property: JsonPropertyName("primaryKeyField")] string? PrimaryKeyField = null
);

public record SelectedSmartObjectFieldDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("type")] string Type = "string",
    [property: JsonPropertyName("masked")] bool Masked = false,
    [property: JsonPropertyName("isRequired")] bool IsRequired = false
);

public record GenerateSmartObjectFromConnectionRequest(
    [property: JsonPropertyName("entityName")] string EntityName,
    [property: JsonPropertyName("smartObjectKey")] string SmartObjectKey,
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("primaryKeyField")] string? PrimaryKeyField = null,
    [property: JsonPropertyName("selectedFields")] List<SelectedSmartObjectFieldDto>? SelectedFields = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("category")] string? Category = null,
    [property: JsonPropertyName("publishImmediately")] bool PublishImmediately = false
);

public record SyncSmartObjectSchemaRequest(
    [property: JsonPropertyName("apply")] bool Apply = false
);

public record SchemaFieldTypeChangeDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("previousType")] string PreviousType,
    [property: JsonPropertyName("newType")] string NewType,
    [property: JsonPropertyName("sourceType")] string SourceType
);

public record SchemaDiffDto(
    [property: JsonPropertyName("smartObjectId")] string SmartObjectId,
    [property: JsonPropertyName("connectionId")] string ConnectionId,
    [property: JsonPropertyName("entityName")] string EntityName,
    [property: JsonPropertyName("addedFields")] IReadOnlyList<DiscoveredFieldDto> AddedFields,
    [property: JsonPropertyName("removedFields")] IReadOnlyList<SmartObjectFieldDto> RemovedFields,
    [property: JsonPropertyName("typeChangedFields")] IReadOnlyList<SchemaFieldTypeChangeDto> TypeChangedFields,
    [property: JsonPropertyName("primaryKeyChanged")] bool PrimaryKeyChanged,
    [property: JsonPropertyName("previousPrimaryKey")] string? PreviousPrimaryKey = null,
    [property: JsonPropertyName("newPrimaryKey")] string? NewPrimaryKey = null,
    [property: JsonPropertyName("hasChanges")] bool HasChanges = false,
    [property: JsonPropertyName("applied")] bool Applied = false
);
