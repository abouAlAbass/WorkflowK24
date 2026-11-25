using System.Globalization;
using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartForms.DTOs;

namespace WorkflowEngine.Application.Features.SmartForms.Validation;

public sealed class SmartFormValidationEngine : ISmartFormValidationEngine
{
    private static readonly JsonSerializerOptions SchemaJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmartFormValidationResult ValidateSubmission(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? submittedData)
    {
        return ValidateInternal(fieldsJson, submittedData, existingData: null, isPatch: false);
    }

    public SmartFormValidationResult ValidatePatch(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? formPatch,
        IReadOnlyDictionary<string, object?>? existingData = null)
    {
        return ValidateInternal(fieldsJson, formPatch, existingData, isPatch: true);
    }

    private static SmartFormValidationResult ValidateInternal(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? submittedData,
        IReadOnlyDictionary<string, object?>? existingData,
        bool isPatch)
    {
        var fields = DeserializeFields(fieldsJson);
        var dataFields = fields
            .Where(f => !string.Equals(f.Type, "section", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var normalizedIncoming = NormalizeDictionary(submittedData);
        var normalizedExisting = NormalizeDictionary(existingData);

        // Build evaluation state for visibility rules (existing/defaults overlaid with incoming values)
        var evalState = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in dataFields)
        {
            if (isPatch && normalizedExisting.TryGetValue(f.Name, out var existingVal))
            {
                evalState[f.Name] = existingVal;
            }
            else if (f.DefaultValue != null)
            {
                evalState[f.Name] = f.DefaultValue;
            }
        }

        foreach (var kv in normalizedIncoming)
        {
            evalState[kv.Key] = kv.Value;
        }

        // 1. Evaluate visibility rules deterministically (6 operators matching SmartFormRenderer.tsx)
        var visibleFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in dataFields)
        {
            if (EvaluateFieldVisibility(field.VisibilityRule, evalState))
            {
                visibleFields.Add(field.Name);
            }
            else
            {
                // Ensure chained visibility rules see hidden parent fields as empty
                evalState[field.Name] = string.Empty;
            }
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? primaryErrorCode = null;

        // 2 & 3. Strip hidden/undeclared phantom keys AND protect readOnly fields from modification
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in dataFields)
        {
            bool isVisible = visibleFields.Contains(field.Name);
            bool wasSubmitted = normalizedIncoming.TryGetValue(field.Name, out object? rawSubmittedValue);

            if (field.ReadOnly)
            {
                object? baseline = isPatch && normalizedExisting.TryGetValue(field.Name, out var prevVal)
                    ? prevVal
                    : field.DefaultValue;

                if (wasSubmitted && IsReadOnlyValueModified(rawSubmittedValue, baseline))
                {
                    AddError(
                        errors,
                        field.Name,
                        $"{SmartFormValidationFailedException.ReadOnlyModifiedCode}: Field '{field.Label}' ({field.Name}) is read-only and cannot be modified.");
                    primaryErrorCode ??= SmartFormValidationFailedException.ReadOnlyModifiedCode;
                }

                // On full submission, keep default value if defined and visible; on patch, never mutate read-only fields
                if (!isPatch && isVisible && !string.IsNullOrWhiteSpace(field.DefaultValue))
                {
                    sanitized[field.Name] = CoerceDefaultValue(field);
                }

                continue;
            }

            // Strip hidden fields (phantom values) from final dictionary
            if (!isVisible)
            {
                continue;
            }

            if (wasSubmitted)
            {
                sanitized[field.Name] = rawSubmittedValue;
            }
        }

        // 4 & 5. Validate required visible fields + data types & constraints
        foreach (var field in dataFields)
        {
            if (field.ReadOnly || !visibleFields.Contains(field.Name))
            {
                continue;
            }

            bool presentInSanitized = sanitized.TryGetValue(field.Name, out object? val);

            // Step 4: Required check (only when field is visible)
            if (field.Required)
            {
                if (!isPatch)
                {
                    if (!presentInSanitized || IsEmptyForRequired(field.Type, val))
                    {
                        AddError(errors, field.Name, $"{field.Label} is required.");
                        continue;
                    }
                }
                else if (presentInSanitized && IsEmptyForRequired(field.Type, val))
                {
                    AddError(errors, field.Name, $"{field.Label} is required and cannot be cleared.");
                    continue;
                }
            }

            // Step 5: Type & constraint validation when a non-empty value was provided
            if (!presentInSanitized || IsSkippableEmptyOptional(field.Type, val))
            {
                continue;
            }

            ValidateFieldValueAndCoerce(field, val, sanitized, errors);
        }

        if (errors.Count > 0)
        {
            var errorDict = errors.ToDictionary(k => k.Key, v => v.Value.ToArray(), StringComparer.Ordinal);
            return new SmartFormValidationResult(
                IsValid: false,
                SanitizedData: sanitized,
                Errors: errorDict,
                ErrorCode: primaryErrorCode ?? SmartFormValidationFailedException.ValidationFailedCode);
        }

        return new SmartFormValidationResult(
            IsValid: true,
            SanitizedData: sanitized,
            Errors: new Dictionary<string, string[]>(StringComparer.Ordinal),
            ErrorCode: null);
    }

    public static bool EvaluateFieldVisibility(
        SmartFormVisibilityRuleDto? rule,
        IReadOnlyDictionary<string, object?> values)
    {
        if (rule == null || string.IsNullOrWhiteSpace(rule.Field))
        {
            return true;
        }

        values.TryGetValue(rule.Field, out object? rawActual);
        string actualStr = ConvertToComparableString(rawActual);
        string expectedStr = rule.Value ?? string.Empty;

        string op = rule.Operator?.Trim().ToLowerInvariant() ?? string.Empty;
        if (op == "is_not_empty")
        {
            return actualStr.Trim().Length > 0;
        }

        double actualNum = 0;
        double expectedNum = 0;
        bool bothNumeric =
            !string.IsNullOrWhiteSpace(actualStr) &&
            !string.IsNullOrWhiteSpace(expectedStr) &&
            double.TryParse(actualStr.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out actualNum) &&
            double.TryParse(expectedStr.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out expectedNum) &&
            !double.IsNaN(actualNum) &&
            !double.IsNaN(expectedNum);

        return op switch
        {
            "greater_than" => bothNumeric
                ? actualNum > expectedNum
                : string.CompareOrdinal(actualStr, expectedStr) > 0,

            "less_than" => bothNumeric
                ? actualNum < expectedNum
                : string.CompareOrdinal(actualStr, expectedStr) < 0,

            "equals" => bothNumeric
                ? actualNum == expectedNum
                : string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),

            "not_equals" => bothNumeric
                ? actualNum != expectedNum
                : !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),

            "contains" => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),

            _ => true
        };
    }

    private static void ValidateFieldValueAndCoerce(
        SmartFormFieldDto field,
        object? value,
        Dictionary<string, object?> sanitized,
        Dictionary<string, List<string>> errors)
    {
        string type = field.Type?.Trim().ToLowerInvariant() ?? "text";

        switch (type)
        {
            case "text":
            case "textarea":
                if (value is not string)
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' must be a valid text string.");
                }
                break;

            case "number":
            case "currency":
                if (!TryCoerceDecimal(value, out decimal numericVal))
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' must be a valid numeric value.");
                }
                else
                {
                    sanitized[field.Name] = numericVal == decimal.Truncate(numericVal) && numericVal is >= long.MinValue and <= long.MaxValue
                        ? (long)numericVal
                        : numericVal;
                }
                break;

            case "date":
                if (value is not string dateStr || !IsValidIsoDate(dateStr))
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' must be a valid ISO date.");
                }
                break;

            case "checkbox":
            case "boolean":
                if (!TryCoerceBoolean(value, out bool boolVal))
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' must be a valid boolean value.");
                }
                else
                {
                    sanitized[field.Name] = boolVal;
                }
                break;

            case "select":
            case "radio":
                if (value is not string selectedStr)
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' selection must be a string.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(field.LookupSmartObjectId) &&
                    field.Options is { Count: > 0 } &&
                    !field.Options.Any(opt => string.Equals(opt, selectedStr, StringComparison.Ordinal)))
                {
                    AddError(
                        errors,
                        field.Name,
                        $"Value '{selectedStr}' is not an allowed option for '{field.Label}'. Allowed: {string.Join(", ", field.Options)}.");
                }
                break;

            case "repeater":
                ValidateAndSanitizeRepeater(field, value, sanitized, errors);
                break;

            case "file":
                if (value is IDictionary<string, object?> attMap)
                {
                    bool hasId = attMap.TryGetValue("id", out var idVal) && idVal is string idStr && !string.IsNullOrWhiteSpace(idStr);
                    bool hasName = attMap.TryGetValue("fileName", out var fnVal) && fnVal is string fnStr && !string.IsNullOrWhiteSpace(fnStr);
                    if (!hasId || !hasName)
                    {
                        AddError(errors, field.Name, $"Field '{field.Label}' contains an invalid file attachment reference.");
                    }
                }
                else
                {
                    AddError(errors, field.Name, $"Field '{field.Label}' must be a valid attachment metadata object.");
                }
                break;
        }
    }

    private static void ValidateAndSanitizeRepeater(
        SmartFormFieldDto field,
        object? value,
        Dictionary<string, object?> sanitized,
        Dictionary<string, List<string>> errors)
    {
        if (value is not IList<object?> rows)
        {
            AddError(errors, field.Name, $"Field '{field.Label}' must be an array of row objects.");
            return;
        }

        var columns = field.RepeaterColumns ?? new List<SmartFormRepeaterColumnDto>();
        var sanitizedRows = new List<Dictionary<string, object?>>(rows.Count);

        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            if (rows[rowIdx] is not IDictionary<string, object?> rowObj)
            {
                AddError(errors, field.Name, $"Row {rowIdx + 1} in '{field.Label}' must be an object.");
                continue;
            }

            var cleanRow = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var col in columns)
            {
                rowObj.TryGetValue(col.Name, out object? cellVal);

                if (col.Required && IsEmptyScalar(cellVal))
                {
                    AddError(errors, field.Name, $"Row {rowIdx + 1}: column '{col.Label}' is required.");
                    continue;
                }

                if (IsEmptyScalar(cellVal))
                {
                    cleanRow[col.Name] = cellVal;
                    continue;
                }

                string colType = col.Type?.Trim().ToLowerInvariant() ?? "text";
                switch (colType)
                {
                    case "number":
                        if (!TryCoerceDecimal(cellVal, out decimal numCell))
                        {
                            AddError(errors, field.Name, $"Row {rowIdx + 1}: column '{col.Label}' must be numeric.");
                        }
                        else
                        {
                            cleanRow[col.Name] = numCell == decimal.Truncate(numCell) && numCell is >= long.MinValue and <= long.MaxValue
                                ? (long)numCell
                                : numCell;
                        }
                        break;

                    case "date":
                        if (cellVal is not string dStr || !IsValidIsoDate(dStr))
                        {
                            AddError(errors, field.Name, $"Row {rowIdx + 1}: column '{col.Label}' must be a valid date.");
                        }
                        else
                        {
                            cleanRow[col.Name] = dStr;
                        }
                        break;

                    default:
                        if (cellVal is not string)
                        {
                            AddError(errors, field.Name, $"Row {rowIdx + 1}: column '{col.Label}' must be text.");
                        }
                        else
                        {
                            cleanRow[col.Name] = cellVal;
                        }
                        break;
                }
            }

            sanitizedRows.Add(cleanRow);
        }

        sanitized[field.Name] = sanitizedRows;
    }

    private static bool IsReadOnlyValueModified(object? incoming, object? baseline)
    {
        string incomingStr = ConvertToComparableString(incoming).Trim();
        string baselineStr = ConvertToComparableString(baseline).Trim();

        if (incomingStr.Length == 0 && baselineStr.Length == 0)
        {
            return false;
        }

        if (decimal.TryParse(incomingStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal inDec) &&
            decimal.TryParse(baselineStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal baseDec))
        {
            return inDec != baseDec;
        }

        if (bool.TryParse(incomingStr, out bool inBool) && bool.TryParse(baselineStr, out bool baseBool))
        {
            return inBool != baseBool;
        }

        return !string.Equals(incomingStr, baselineStr, StringComparison.Ordinal);
    }

    private static object? CoerceDefaultValue(SmartFormFieldDto field)
    {
        if (field.DefaultValue == null) return null;
        string type = field.Type?.Trim().ToLowerInvariant() ?? "text";

        if ((type is "number" or "currency") && TryCoerceDecimal(field.DefaultValue, out decimal dec))
        {
            return dec == decimal.Truncate(dec) && dec is >= long.MinValue and <= long.MaxValue ? (long)dec : dec;
        }

        if ((type is "checkbox" or "boolean") && TryCoerceBoolean(field.DefaultValue, out bool b))
        {
            return b;
        }

        return field.DefaultValue;
    }

    private static bool IsEmptyForRequired(string? fieldType, object? val)
    {
        if (val == null) return true;
        string type = fieldType?.Trim().ToLowerInvariant() ?? "text";

        if (type is "checkbox" or "boolean")
        {
            return val is string s && string.IsNullOrWhiteSpace(s);
        }

        if (type == "repeater")
        {
            return val is not IList<object?> list || list.Count == 0;
        }

        if (val is string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        return false;
    }

    private static bool IsSkippableEmptyOptional(string? fieldType, object? val)
    {
        if (val == null) return true;
        string type = fieldType?.Trim().ToLowerInvariant() ?? "text";

        if (type == "repeater")
        {
            return val is IList<object?> { Count: 0 };
        }

        return val is string str && string.IsNullOrWhiteSpace(str);
    }

    private static bool IsEmptyScalar(object? val)
    {
        return val == null || (val is string s && string.IsNullOrWhiteSpace(s));
    }

    private static bool TryCoerceDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case decimal m:
                number = m;
                return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                number = (decimal)d;
                return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                number = (decimal)f;
                return true;
            case string s when !string.IsNullOrWhiteSpace(s):
                return decimal.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryCoerceBoolean(object? value, out bool result)
    {
        switch (value)
        {
            case bool b:
                result = b;
                return true;
            case string s when bool.TryParse(s.Trim(), out bool parsed):
                result = parsed;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool IsValidIsoDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string trimmed = raw.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out _) && trimmed.Length >= 8 && char.IsDigit(trimmed[0]);
    }

    private static string ConvertToComparableString(object? val)
    {
        return val switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => val.ToString() ?? string.Empty
        };
    }

    private static List<SmartFormFieldDto> DeserializeFields(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
        {
            return new List<SmartFormFieldDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<SmartFormFieldDto>>(fieldsJson, SchemaJsonOptions)
                ?? new List<SmartFormFieldDto>();
        }
        catch
        {
            return new List<SmartFormFieldDto>();
        }
    }

    private static Dictionary<string, object?> NormalizeDictionary(IReadOnlyDictionary<string, object?>? source)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
        {
            return dict;
        }

        foreach (var kv in source)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
            {
                dict[kv.Key] = NormalizeValue(kv.Value);
            }
        }

        return dict;
    }

    private static object? NormalizeValue(object? val)
    {
        if (val is JsonElement el)
        {
            return ConvertJsonElement(el);
        }

        if (val is IDictionary<string, object?> map)
        {
            return map.ToDictionary(k => k.Key, v => NormalizeValue(v.Value), StringComparer.Ordinal);
        }

        if (val is IEnumerable<object?> seq && val is not string)
        {
            return seq.Select(NormalizeValue).ToList();
        }

        return val;
    }

    private static object? ConvertJsonElement(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.TryGetInt64(out long l)
                ? l
                : el.TryGetDecimal(out decimal m)
                    ? m
                    : el.GetDouble(),
            JsonValueKind.Array => el.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => el.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.Ordinal),
            _ => el.GetRawText()
        };
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = new List<string>();
            errors[key] = list;
        }

        list.Add(message);
    }
}
