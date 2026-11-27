using System.Text.Json;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Services.SmartObjects;

/// <summary>
/// Built-in SmartObject provider backed by the relational SmartObjectRecords table.
/// Supports read, create, update, delete, and custom operations with normalized BUSINESS errors.
/// </summary>
public class InternalStoreSmartObjectProvider : ISmartObjectProvider
{
    private readonly ISmartObjectRepository _repository;

    public InternalStoreSmartObjectProvider(ISmartObjectRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public string ProviderName => "InternalStore";

    public bool CanHandle(string providerName) =>
        string.IsNullOrWhiteSpace(providerName) ||
        string.Equals(providerName.Trim(), "InternalStore", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(providerName.Trim(), "internal", StringComparison.OrdinalIgnoreCase);

    public async Task<SmartObjectProviderResult> ExecuteAsync(SmartObjectProviderContext context, CancellationToken ct = default)
    {
        string kind = (context.Method.Kind ?? "read").Trim().ToLowerInvariant();
        string pkField = ResolvePrimaryKeyField(context);

        return kind switch
        {
            "read" => await ExecuteReadAsync(context, pkField, ct),
            "create" => await ExecuteCreateAsync(context, pkField, ct),
            "update" => await ExecuteUpdateAsync(context, pkField, ct),
            "delete" => await ExecuteDeleteAsync(context, pkField, ct),
            "custom" => await ExecuteCustomAsync(context, pkField, ct),
            _ => SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConfigInvalid,
                $"Unsupported InternalStore method kind '{context.Method.Kind}'.")
        };
    }

    private async Task<SmartObjectProviderResult> ExecuteReadAsync(
        SmartObjectProviderContext context,
        string pkField,
        CancellationToken ct)
    {
        string? recordKey = ExtractRecordKey(context.Inputs, pkField);
        if (string.IsNullOrWhiteSpace(recordKey))
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.Validation,
                $"Missing record key input ('{pkField}') for read method '{context.Method.Name}' on '{context.SmartObject.ObjectRef}'.");
        }

        var record = await _repository.GetRecordByKeyAsync(context.SmartObject.Id, recordKey, ct);
        if (record == null)
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.NotFound,
                $"Record '{recordKey}' was not found in SmartObject '{context.SmartObject.ObjectRef}'.");
        }

        var data = DeserializeRecordData(record.DataJson);
        data["recordId"] = record.Id;
        data["recordKey"] = record.RecordKey;
        return SmartObjectProviderResult.Success(data);
    }

    private async Task<SmartObjectProviderResult> ExecuteCreateAsync(
        SmartObjectProviderContext context,
        string pkField,
        CancellationToken ct)
    {
        string recordKey = ExtractRecordKey(context.Inputs, pkField)
            ?? $"REC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var existing = await _repository.GetRecordByKeyAsync(context.SmartObject.Id, recordKey, ct);
        if (existing != null)
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.Conflict,
                $"Record '{recordKey}' already exists in SmartObject '{context.SmartObject.ObjectRef}'.");
        }

        var payload = new Dictionary<string, object?>(context.Inputs, StringComparer.OrdinalIgnoreCase);
        if (!payload.ContainsKey(pkField))
        {
            payload[pkField] = recordKey;
        }

        string dataJson = JsonSerializer.Serialize(payload);
        var entity = new SmartObjectRecordEntity(context.SmartObject.Id, recordKey, dataJson);
        var saved = await _repository.AddRecordAsync(entity, ct);

        payload["recordId"] = saved.Id;
        payload["recordKey"] = saved.RecordKey;
        return SmartObjectProviderResult.Success(payload);
    }

    private async Task<SmartObjectProviderResult> ExecuteUpdateAsync(
        SmartObjectProviderContext context,
        string pkField,
        CancellationToken ct)
    {
        string? recordKey = ExtractRecordKey(context.Inputs, pkField);
        if (string.IsNullOrWhiteSpace(recordKey))
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.Validation,
                $"Missing record key input ('{pkField}') for update method '{context.Method.Name}'.");
        }

        var existing = await _repository.GetRecordByKeyAsync(context.SmartObject.Id, recordKey, ct);
        if (existing == null)
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.NotFound,
                $"Record '{recordKey}' was not found in SmartObject '{context.SmartObject.ObjectRef}'.");
        }

        var merged = DeserializeRecordData(existing.DataJson);
        foreach (var kv in context.Inputs)
        {
            merged[kv.Key] = kv.Value;
        }

        existing.UpdateData(JsonSerializer.Serialize(merged));
        await _repository.UpdateRecordAsync(existing, ct);

        merged["recordId"] = existing.Id;
        merged["recordKey"] = existing.RecordKey;
        return SmartObjectProviderResult.Success(merged);
    }

    private async Task<SmartObjectProviderResult> ExecuteDeleteAsync(
        SmartObjectProviderContext context,
        string pkField,
        CancellationToken ct)
    {
        string? recordKey = ExtractRecordKey(context.Inputs, pkField);
        if (string.IsNullOrWhiteSpace(recordKey))
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.Validation,
                $"Missing record key input ('{pkField}') for delete method '{context.Method.Name}'.");
        }

        bool deleted = await _repository.DeleteRecordAsync(context.SmartObject.Id, recordKey, ct);
        if (!deleted)
        {
            return SmartObjectProviderResult.BusinessError(
                SmartObjectErrorCodes.NotFound,
                $"Record '{recordKey}' was not found in SmartObject '{context.SmartObject.ObjectRef}'.");
        }

        return SmartObjectProviderResult.Success(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["deleted"] = true,
            ["recordKey"] = recordKey
        });
    }

    private async Task<SmartObjectProviderResult> ExecuteCustomAsync(
        SmartObjectProviderContext context,
        string pkField,
        CancellationToken ct)
    {
        string? recordKey = ExtractRecordKey(context.Inputs, pkField);
        if (!string.IsNullOrWhiteSpace(recordKey))
        {
            var existing = await _repository.GetRecordByKeyAsync(context.SmartObject.Id, recordKey, ct);
            if (existing != null)
            {
                var data = DeserializeRecordData(existing.DataJson);
                data["recordId"] = existing.Id;
                data["recordKey"] = existing.RecordKey;
                return SmartObjectProviderResult.Success(data);
            }
        }

        var output = new Dictionary<string, object?>(context.Inputs, StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "executed"
        };
        return SmartObjectProviderResult.Success(output);
    }

    private static string ResolvePrimaryKeyField(SmartObjectProviderContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Method.Binding?.RecordKeyInput))
        {
            return context.Method.Binding.RecordKeyInput.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(context.SmartObject.FieldsJson);
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (elem.TryGetProperty("isPrimaryKey", out var isPk) && isPk.ValueKind == JsonValueKind.True &&
                    elem.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    string? name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
        }
        catch
        {
            // Fallback to default key
        }

        return "id";
    }

    private static string? ExtractRecordKey(Dictionary<string, object?> inputs, string pkField)
    {
        foreach (var candidate in new[] { pkField, "recordKey", "id", "key" })
        {
            var match = inputs.FirstOrDefault(kv => string.Equals(kv.Key, candidate, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null && match.Value != null)
            {
                string? str = ConvertToString(match.Value);
                if (!string.IsNullOrWhiteSpace(str))
                {
                    return str;
                }
            }
        }

        return null;
    }

    private static string? ConvertToString(object? value)
    {
        if (value == null) return null;
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return value.ToString();
    }

    private static Dictionary<string, object?> DeserializeRecordData(string json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.Clone();
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }

        return result;
    }
}
