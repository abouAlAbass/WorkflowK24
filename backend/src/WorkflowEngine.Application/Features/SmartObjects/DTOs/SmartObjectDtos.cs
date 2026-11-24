using System.Text.Json.Serialization;

namespace WorkflowEngine.Application.Features.SmartObjects.DTOs;

public record SmartObjectFieldDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("isPrimaryKey")] bool? IsPrimaryKey = false,
    [property: JsonPropertyName("isRequired")] bool? IsRequired = false
);

public record SmartObjectDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("recordsCount")] int RecordsCount,
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto> Fields
);
