using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services.Executors;

/// <summary>
/// Safe, eval-free template resolver for workflow node configurations.
/// Supports {{vars.x}}, {{vars.nested.prop}}, {{execution_key}}, {{instanceId}}, {{workflowKey}}, {{initiatorId}},
/// and direct variable fallback {{x}}.
/// </summary>
public static partial class SafeTemplateResolver
{
    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    public static string ResolveString(string? template, WorkflowInstance instance, string executionKey)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template ?? string.Empty;
        }

        JsonDocument? doc = ParseContextVariables(instance.ContextVariablesJson);
        using (doc)
        {
            return PlaceholderRegex().Replace(template, match =>
            {
                string key = match.Groups[1].Value;
                if (TryResolveToken(key, instance, executionKey, doc, out string resolved))
                {
                    return resolved;
                }

                return match.Value;
            });
        }
    }

    public static JsonNode? ResolveJsonElement(JsonElement element, WorkflowInstance instance, string executionKey)
    {
        JsonDocument? doc = ParseContextVariables(instance.ContextVariablesJson);
        using (doc)
        {
            return ResolveNodeRecursive(element, instance, executionKey, doc);
        }
    }

    private static JsonNode? ResolveNodeRecursive(
        JsonElement element,
        WorkflowInstance instance,
        string executionKey,
        JsonDocument? contextDoc)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = ResolveNodeRecursive(prop.Value, instance, executionKey, contextDoc);
                }
                return obj;

            case JsonValueKind.Array:
                var arr = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(ResolveNodeRecursive(item, instance, executionKey, contextDoc));
                }
                return arr;

            case JsonValueKind.String:
                string raw = element.GetString() ?? string.Empty;
                if (!raw.Contains("{{", StringComparison.Ordinal))
                {
                    return JsonValue.Create(raw);
                }

                // If the entire string is a single placeholder, preserve typed JSON value when possible
                var exactMatch = PlaceholderRegex().Match(raw);
                if (exactMatch.Success && exactMatch.Index == 0 && exactMatch.Length == raw.Length)
                {
                    string key = exactMatch.Groups[1].Value;
                    if (TryResolveTypedToken(key, instance, executionKey, contextDoc, out var typedNode))
                    {
                        return typedNode;
                    }
                }

                string interpolated = PlaceholderRegex().Replace(raw, m =>
                    TryResolveToken(m.Groups[1].Value, instance, executionKey, contextDoc, out string val)
                        ? val
                        : m.Value);
                return JsonValue.Create(interpolated);

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return JsonNode.Parse(element.GetRawText());

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static bool TryResolveTypedToken(
        string key,
        WorkflowInstance instance,
        string executionKey,
        JsonDocument? contextDoc,
        out JsonNode? value)
    {
        if (key.Equals("execution_key", StringComparison.OrdinalIgnoreCase))
        {
            value = JsonValue.Create(executionKey);
            return true;
        }
        if (key.Equals("instanceId", StringComparison.OrdinalIgnoreCase))
        {
            value = JsonValue.Create(instance.InstanceId);
            return true;
        }
        if (key.Equals("workflowKey", StringComparison.OrdinalIgnoreCase))
        {
            value = JsonValue.Create(instance.WorkflowKey);
            return true;
        }
        if (key.Equals("initiatorId", StringComparison.OrdinalIgnoreCase) || key.Equals("requester", StringComparison.OrdinalIgnoreCase))
        {
            value = JsonValue.Create(instance.InitiatorId);
            return true;
        }

        string varPath = key.StartsWith("vars.", StringComparison.OrdinalIgnoreCase)
            ? key[5..]
            : key;

        if (contextDoc != null && TryResolveProperty(contextDoc.RootElement, varPath, out var element))
        {
            value = element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined
                ? null
                : JsonNode.Parse(element.GetRawText());
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryResolveToken(
        string key,
        WorkflowInstance instance,
        string executionKey,
        JsonDocument? contextDoc,
        out string value)
    {
        if (key.Equals("execution_key", StringComparison.OrdinalIgnoreCase))
        {
            value = executionKey;
            return true;
        }
        if (key.Equals("instanceId", StringComparison.OrdinalIgnoreCase))
        {
            value = instance.InstanceId;
            return true;
        }
        if (key.Equals("workflowKey", StringComparison.OrdinalIgnoreCase))
        {
            value = instance.WorkflowKey;
            return true;
        }
        if (key.Equals("initiatorId", StringComparison.OrdinalIgnoreCase) || key.Equals("requester", StringComparison.OrdinalIgnoreCase))
        {
            value = instance.InitiatorId;
            return true;
        }

        string varPath = key.StartsWith("vars.", StringComparison.OrdinalIgnoreCase)
            ? key[5..]
            : key;

        if (contextDoc != null && TryResolveProperty(contextDoc.RootElement, varPath, out var element))
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => element.GetRawText()
            };
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static JsonDocument? ParseContextVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryResolveProperty(JsonElement root, string path, out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        JsonElement current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (current.TryGetProperty(segment, out var exact))
            {
                current = exact;
                continue;
            }

            bool found = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (string.Equals(prop.Name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    current = prop.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        value = current;
        return true;
    }
}
