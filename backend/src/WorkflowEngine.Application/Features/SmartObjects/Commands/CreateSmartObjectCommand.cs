using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Commands;

public record CreateSmartObjectCommand(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("fields")] List<SmartObjectFieldDto>? Fields
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

        var existing = await repository.GetByIdAsync(id, ct);
        if (existing != null)
        {
            existing.UpdateMetadata(displayName, description, category, fieldsJson);
            await repository.UpdateAsync(existing, ct);
            return new SmartObjectDto(existing.Id, existing.Name, existing.DisplayName, existing.Description, existing.Category, existing.RecordsCount, fields);
        }

        var entity = new SmartObjectEntity(
            id,
            command.Name,
            displayName,
            description,
            category,
            fieldsJson,
            recordsCount: 0
        );

        await repository.AddAsync(entity, ct);

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
