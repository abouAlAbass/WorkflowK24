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
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto>? Fields,
    [property: JsonPropertyName("methods")] List<SmartObjectMethodDto>? Methods = null
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
        string? methodsJson = command.Methods != null ? JsonSerializer.Serialize(command.Methods) : null;

        entity.UpdateMetadata(
            command.DisplayName ?? entity.DisplayName,
            command.Description ?? entity.Description,
            command.Category ?? entity.Category,
            fieldsJson,
            methodsJson
        );

        await repository.UpdateAsync(entity, ct);

        return CreateSmartObjectHandler.MapToDto(entity);
    }
}

public record PublishSmartObjectCommand(string Id, string PublishedBy);

public static class PublishSmartObjectHandler
{
    public static async Task<SmartObjectDto?> Handle(
        PublishSmartObjectCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, ct);
        if (entity == null) return null;

        entity.Publish(command.PublishedBy);
        await repository.UpdateAsync(entity, ct);

        return CreateSmartObjectHandler.MapToDto(entity);
    }
}

public record ArchiveSmartObjectCommand(string Id);

public static class ArchiveSmartObjectHandler
{
    public static async Task<SmartObjectDto?> Handle(
        ArchiveSmartObjectCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, ct);
        if (entity == null) return null;

        entity.Archive();
        await repository.UpdateAsync(entity, ct);

        return CreateSmartObjectHandler.MapToDto(entity);
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
