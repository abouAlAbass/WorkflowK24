using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkflowEngine.Application.Services.Executors;

/// <summary>
/// Simple, safe JSONPath extractor supporting expressions like "$.a.b", "$.items[0].id", or "$".
/// Returns typed JsonElement values without eval or dynamic code execution.
/// </summary>
public static partial class SimpleJsonPathExtractor
{
    [GeneratedRegex(@"^([^\[\]]+)(?:\[(\d+)\])?$", RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();

    public static bool TryExtract(JsonElement root, string? path, out JsonElement result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string trimmed = path.Trim();
        if (trimmed == "$")
        {
            result = root;
            return true;
        }

        if (trimmed.StartsWith("$.", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith("$", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].TrimStart('.');
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            result = root;
            return true;
        }

        JsonElement current = root;
        var segments = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var match = SegmentRegex().Match(segment);
            if (!match.Success)
            {
                return false;
            }

            string propName = match.Groups[1].Value;
            bool hasIndex = match.Groups[2].Success;

            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyCaseInsensitive(current, propName, out var next))
            {
                return false;
            }

            current = next;

            if (hasIndex)
            {
                if (current.ValueKind != JsonValueKind.Array ||
                    !int.TryParse(match.Groups[2].Value, out int index) ||
                    index < 0 ||
                    index >= current.GetArrayLength())
                {
                    return false;
                }

                current = current[index];
            }
        }

        result = current;
        return true;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
