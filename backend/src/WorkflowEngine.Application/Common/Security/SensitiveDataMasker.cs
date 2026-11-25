using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorkflowEngine.Application.Common.Security;

/// <summary>
/// Masks sensitive workflow variables and event JSON payloads unless the caller
/// holds an Admin role or the explicit 'instance.view_sensitive' permission.
/// </summary>
public static class SensitiveDataMasker
{
    public const string MaskValue = "***";
    public const string ViewSensitivePermission = "instance.view_sensitive";

    private static readonly string[] DefaultSensitiveKeywords =
    {
        "password",
        "secret",
        "token",
        "iban",
        "salary"
    };

    private static readonly HashSet<string> AdminRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "BPM Administrator"
    };

    public static bool CanViewSensitive(IReadOnlyList<string>? roles, IReadOnlyList<string>? permissions)
    {
        if (roles != null && roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoleNames.Contains(r.Trim())))
        {
            return true;
        }

        if (permissions != null && permissions.Any(p =>
            !string.IsNullOrWhiteSpace(p) &&
            string.Equals(p.Trim(), ViewSensitivePermission, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public static bool IsSensitiveKey(string key, ISet<string>? explicitSensitiveNames = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (explicitSensitiveNames != null && explicitSensitiveNames.Contains(key))
        {
            return true;
        }

        foreach (var keyword in DefaultSensitiveKeywords)
        {
            if (key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static Dictionary<string, object?> MaskVariables(
        string? contextVariablesJson,
        ISet<string>? explicitSensitiveNames,
        bool canViewSensitive)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(contextVariablesJson))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(contextVariablesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!canViewSensitive && IsSensitiveKey(prop.Name, explicitSensitiveNames))
                {
                    result[prop.Name] = MaskValue;
                }
                else
                {
                    result[prop.Name] = ConvertElement(prop.Value, explicitSensitiveNames, canViewSensitive);
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }

        return result;
    }

    public static string? MaskPayloadJson(
        string? payloadJson,
        ISet<string>? explicitSensitiveNames,
        bool canViewSensitive)
    {
        if (canViewSensitive || string.IsNullOrWhiteSpace(payloadJson))
        {
            return payloadJson;
        }

        try
        {
            var node = JsonNode.Parse(payloadJson);
            if (node == null)
            {
                return payloadJson;
            }

            MaskNodeInPlace(node, explicitSensitiveNames);
            return node.ToJsonString();
        }
        catch
        {
            return payloadJson;
        }
    }

    private static void MaskNodeInPlace(JsonNode node, ISet<string>? explicitSensitiveNames)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).ToList();
            foreach (var key in keys)
            {
                if (IsSensitiveKey(key, explicitSensitiveNames))
                {
                    obj[key] = JsonValue.Create(MaskValue);
                }
                else if (obj[key] is JsonNode child)
                {
                    MaskNodeInPlace(child, explicitSensitiveNames);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                {
                    MaskNodeInPlace(item, explicitSensitiveNames);
                }
            }
        }
    }

    private static object? ConvertElement(
        JsonElement element,
        ISet<string>? explicitSensitiveNames,
        bool canViewSensitive)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Object => ConvertObject(element, explicitSensitiveNames, canViewSensitive),
            JsonValueKind.Array => element.EnumerateArray().Select(e => ConvertElement(e, explicitSensitiveNames, canViewSensitive)).ToList(),
            _ => element.ToString()
        };
    }

    private static Dictionary<string, object?> ConvertObject(
        JsonElement element,
        ISet<string>? explicitSensitiveNames,
        bool canViewSensitive)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            if (!canViewSensitive && IsSensitiveKey(prop.Name, explicitSensitiveNames))
            {
                dict[prop.Name] = MaskValue;
            }
            else
            {
                dict[prop.Name] = ConvertElement(prop.Value, explicitSensitiveNames, canViewSensitive);
            }
        }
        return dict;
    }
}
