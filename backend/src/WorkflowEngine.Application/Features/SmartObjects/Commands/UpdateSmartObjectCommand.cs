using System.Text.Json;
using System.Text.Json.Serialization;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Commands;

public record UpdateSmartObjectCommand(
    string Id,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto>? Fields
);

public static class UpdateSmartObjectHandler
{
    public static async Task<SmartObjectDto?> Handle(
        UpdateSmartObjectCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, ct);
        if (entity == null) return null;

        string? fieldsJson = command.Fields != null ? JsonSerializer.Serialize(command.Fields) : null;
        entity.UpdateMetadata(
            command.DisplayName ?? entity.DisplayName,
            command.Description ?? entity.Description,
            command.Category ?? entity.Category,
            fieldsJson
        );

        await repository.UpdateAsync(entity, ct);

        var fields = new List<SmartObjectFieldDto>();
        try
        {
            fields = JsonSerializer.Deserialize<List<SmartObjectFieldDto>>(entity.FieldsJson) ?? new();
        }
        catch { }

        return new SmartObjectDto(
            entity.Id,
            entity.Name,
            entity.DisplayName,
            entity.Description,
            entity.Category,
            entity.RecordsCount,
            fields
        );
    }
}

public record DeleteSmartObjectCommand(string Id);

public static class DeleteSmartObjectHandler
{
    public static async Task<bool> Handle(
        DeleteSmartObjectCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        return await repository.DeleteAsync(command.Id, ct);
    }
}
