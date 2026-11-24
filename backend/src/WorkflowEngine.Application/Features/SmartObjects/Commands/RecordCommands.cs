using System.Text.Json;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Commands;

public record AddSmartObjectRecordCommand(string SmartObjectId, Dictionary<string, object?> Record);

public static class AddSmartObjectRecordHandler
{
    public static async Task<Dictionary<string, object?>> Handle(
        AddSmartObjectRecordCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var so = await repository.GetByIdAsync(command.SmartObjectId, ct)
            ?? throw new KeyNotFoundException($"SmartObject with ID '{command.SmartObjectId}' not found.");

        // Extract primary key
        string pkField = "id";
        try
        {
            using var doc = JsonDocument.Parse(so.FieldsJson);
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (elem.TryGetProperty("isPrimaryKey", out var isPk) && isPk.GetBoolean())
                {
                    pkField = elem.GetProperty("name").GetString() ?? pkField;
                    break;
                }
            }
        }
        catch { }

        string recordKey = command.Record.TryGetValue(pkField, out var pkVal) && pkVal != null
            ? pkVal.ToString()!
            : Guid.NewGuid().ToString("N");

        string dataJson = JsonSerializer.Serialize(command.Record);

        var existing = await repository.GetRecordByKeyAsync(command.SmartObjectId, recordKey, ct);
        if (existing != null)
        {
            existing.UpdateData(dataJson);
            await repository.UpdateRecordAsync(existing, ct);
        }
        else
        {
            var newRecord = new SmartObjectRecordEntity(command.SmartObjectId, recordKey, dataJson);
            await repository.AddRecordAsync(newRecord, ct);
        }

        return command.Record;
    }
}

public record UpdateSmartObjectRecordCommand(string SmartObjectId, string PrimaryKeyVal, Dictionary<string, object?> Record);

public static class UpdateSmartObjectRecordHandler
{
    public static async Task<Dictionary<string, object?>?> Handle(
        UpdateSmartObjectRecordCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var record = await repository.GetRecordByKeyAsync(command.SmartObjectId, command.PrimaryKeyVal, ct);
        if (record == null)
        {
            // If not found, add it
            return await AddSmartObjectRecordHandler.Handle(
                new AddSmartObjectRecordCommand(command.SmartObjectId, command.Record),
                repository,
                ct);
        }

        record.UpdateData(JsonSerializer.Serialize(command.Record));
        await repository.UpdateRecordAsync(record, ct);
        return command.Record;
    }
}

public record DeleteSmartObjectRecordCommand(string SmartObjectId, string PrimaryKeyVal);

public static class DeleteSmartObjectRecordHandler
{
    public static async Task<bool> Handle(
        DeleteSmartObjectRecordCommand command,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        return await repository.DeleteRecordAsync(command.SmartObjectId, command.PrimaryKeyVal, ct);
    }
}
