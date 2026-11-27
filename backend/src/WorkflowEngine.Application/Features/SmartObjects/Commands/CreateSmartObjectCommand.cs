using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Commands;

public record CreateSmartObjectCommand(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto>? Fields,
    [property: JsonPropertyName("version")] int? Version = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("methods")] List<SmartObjectMethodDto>? Methods = null,
    [property: JsonPropertyName("publishImmediately")] bool PublishImmediately = false,
    [property: JsonIgnore] string? PublishedBy = null
);

public class CreateSmartObjectValidator : AbstractValidator<CreateSmartObjectCommand>
{
    public CreateSmartObjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public static class CreateSmartObjectHandler
{
    public static async Task<SmartObjectDto> Handle(
        CreateSmartObjectCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        string id = !string.IsNullOrWhiteSpace(command.Id) ? command.Id : $"so-{Guid.NewGuid():N}";
        string displayName = !string.IsNullOrWhiteSpace(command.DisplayName) ? command.DisplayName : command.Name;
        string description = command.Description ?? string.Empty;
        string category = !string.IsNullOrWhiteSpace(command.Category) ? command.Category : "Custom";
        var fields = command.Fields ?? new List<SmartObjectFieldDto>();
        string fieldsJson = JsonSerializer.Serialize(fields);
        var methods = command.Methods ?? new List<SmartObjectMethodDto>();
        string methodsJson = JsonSerializer.Serialize(methods);

        var existing = await repository.GetByIdAsync(id, ct);
        if (existing != null)
        {
            existing.UpdateMetadata(displayName, description, category, fieldsJson, methodsJson);
            if (command.PublishImmediately || string.Equals(command.Status, "Published", StringComparison.OrdinalIgnoreCase))
            {
                existing.Publish(command.PublishedBy);
            }

            await repository.UpdateAsync(existing, ct);
            return MapToDto(existing, fields, methods);
        }

        int latestVersion = await repository.GetLatestVersionNumberAsync(command.Name.Trim(), ct);
        int version = command.Version.HasValue && command.Version.Value >= 1
            ? command.Version.Value
            : latestVersion + 1;

        var status = command.PublishImmediately || string.Equals(command.Status, "Published", StringComparison.OrdinalIgnoreCase)
            ? SmartObjectStatus.Published
            : SmartObjectStatus.Draft;

        var entity = new SmartObjectEntity(
            id,
            command.Name,
            displayName,
            description,
            category,
            fieldsJson,
            recordsCount: 0,
            version: version,
            status: status,
            methodsJson: methodsJson,
            publishedBy: status == SmartObjectStatus.Published ? command.PublishedBy : null
        );

        await repository.AddAsync(entity, ct);

        return MapToDto(entity, fields, methods);
    }

    internal static SmartObjectDto MapToDto(
        SmartObjectEntity entity,
        List<SmartObjectFieldDto>? fieldsOverride = null,
        List<SmartObjectMethodDto>? methodsOverride = null)
    {
        var fields = fieldsOverride ?? DeserializeList<SmartObjectFieldDto>(entity.FieldsJson);
        var methods = methodsOverride ?? DeserializeList<SmartObjectMethodDto>(entity.MethodsJson);

        return new SmartObjectDto(
            entity.Id,
            entity.Name,
            entity.DisplayName,
            entity.Description,
            entity.Category,
            entity.RecordsCount,
            fields,
            entity.Version,
            entity.Status.ToString(),
            entity.ObjectRef,
            methods,
            entity.PublishedAt,
            entity.PublishedBy,
            entity.SourceConnectionId,
            entity.SourceEntityName,
            entity.PrimaryKeyField
        );
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
