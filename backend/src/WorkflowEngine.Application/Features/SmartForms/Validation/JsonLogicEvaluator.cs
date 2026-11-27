using System.Globalization;
using System.Text.Json;

namespace WorkflowEngine.Application.Features.SmartForms.Validation;

/// <summary>
/// Deterministic, zero-eval JSONLogic evaluator shared by SmartForm rules and calculated fields.
/// Semantics are mirrored identically in TypeScript (src/utils/jsonLogicEngine.ts).
/// Supports standard JSONLogic operators plus enterprise repeater aggregations (sum, avg, count, round, contains).
/// </summary>
public static class JsonLogicEvaluator
{
    private const int MaxDepth = 64;

    public static object? Evaluate(JsonElement? rule, IReadOnlyDictionary<string, object?> data)
    {
        if (rule is null || rule.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return EvaluateElement(rule.Value, data, depth: 0);
    }

    public static bool EvaluateBool(JsonElement? rule, IReadOnlyDictionary<string, object?> data)
    {
        return IsTruthy(Evaluate(rule, data));
    }

    public static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            long l => l != 0,
            decimal m => m != 0m,
            double d => !double.IsNaN(d) && d != 0d,
            float f => !float.IsNaN(f) && f != 0f,
            IList<object?> list => list.Count > 0,
            System.Collections.ICollection col => col.Count > 0,
            IEnumerable<object?> seq => seq.Any(),
            _ => true
        };
    }

    private static object? EvaluateElement(JsonElement el, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException("JSONLogic expression exceeded maximum nesting depth.");
        }

        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                return el.TryGetInt64(out long l)
                    ? l
                    : el.TryGetDecimal(out decimal m)
                        ? m
                        : el.GetDouble();
            case JsonValueKind.Array:
                var arr = new List<object?>();
                foreach (var item in el.EnumerateArray())
                {
                    arr.Add(EvaluateElement(item, data, depth + 1));
                }
                return arr;
            case JsonValueKind.Object:
                return EvaluateOperation(el, data, depth + 1);
            default:
                return null;
        }
    }

    private static object? EvaluateOperation(JsonElement obj, IReadOnlyDictionary<string, object?> data, int depth)
    {
        using var enumerator = obj.EnumerateObject();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var prop = enumerator.Current;
        string op = prop.Name;
        JsonElement rawArgs = prop.Value;

        // Operators with lazy/special argument evaluation
        switch (op)
        {
            case "var":
                return EvaluateVar(rawArgs, data, depth);

            case "and":
                return EvaluateAnd(rawArgs, data, depth);

            case "or":
                return EvaluateOr(rawArgs, data, depth);

            case "if":
            case "?:":
                return EvaluateIf(rawArgs, data, depth);

            case "sum":
                return EvaluateAggregation(rawArgs, data, depth, AggregationKind.Sum);

            case "avg":
                return EvaluateAggregation(rawArgs, data, depth, AggregationKind.Avg);

            case "count":
                return EvaluateCount(rawArgs, data, depth);

            case "map":
                return EvaluateMap(rawArgs, data, depth);

            case "filter":
                return EvaluateFilter(rawArgs, data, depth);

            case "reduce":
                return EvaluateReduce(rawArgs, data, depth);
        }

        // Eagerly evaluated operators
        var args = EvaluateArgsList(rawArgs, data, depth);

        return op switch
        {
            "!" => !IsTruthy(args.Count > 0 ? args[0] : null),
            "!!" => IsTruthy(args.Count > 0 ? args[0] : null),
            "==" => args.Count >= 2 && LooseEquals(args[0], args[1]),
            "!=" => args.Count >= 2 && !LooseEquals(args[0], args[1]),
            "===" => args.Count >= 2 && StrictEquals(args[0], args[1]),
            "!==" => args.Count >= 2 && !StrictEquals(args[0], args[1]),
            ">" => EvaluateComparison(args, (a, b) => a > b, (a, b) => string.CompareOrdinal(a, b) > 0),
            ">=" => EvaluateComparison(args, (a, b) => a >= b, (a, b) => string.CompareOrdinal(a, b) >= 0),
            "<" => EvaluateComparison(args, (a, b) => a < b, (a, b) => string.CompareOrdinal(a, b) < 0),
            "<=" => EvaluateComparison(args, (a, b) => a <= b, (a, b) => string.CompareOrdinal(a, b) <= 0),
            "+" => EvaluateAdd(args),
            "-" => EvaluateSubtract(args),
            "*" => EvaluateMultiply(args),
            "/" => EvaluateDivide(args),
            "%" => EvaluateModulo(args),
            "min" => EvaluateMinMax(args, isMax: false),
            "max" => EvaluateMinMax(args, isMax: true),
            "round" => EvaluateRound(args),
            "in" => EvaluateIn(args),
            "contains" => EvaluateContains(args),
            "cat" => string.Concat(args.Select(ToComparableString)),
            "is_not_empty" or "isNotEmpty" => args.Count > 0 && ToComparableString(args[0]).Trim().Length > 0,
            _ => null
        };
    }

    private static List<object?> EvaluateArgsList(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        var list = new List<object?>();
        if (rawArgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rawArgs.EnumerateArray())
            {
                list.Add(EvaluateElement(item, data, depth));
            }
        }
        else
        {
            list.Add(EvaluateElement(rawArgs, data, depth));
        }

        return list;
    }

    private static object? EvaluateVar(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        string? path;
        object? defaultValue = null;

        if (rawArgs.ValueKind == JsonValueKind.Array)
        {
            var items = rawArgs.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                return data;
            }

            path = ToComparableString(EvaluateElement(items[0], data, depth));
            if (items.Count > 1)
            {
                defaultValue = EvaluateElement(items[1], data, depth);
            }
        }
        else
        {
            var evaluated = EvaluateElement(rawArgs, data, depth);
            if (evaluated is null)
            {
                return data;
            }
            path = ToComparableString(evaluated);
        }

        if (string.IsNullOrEmpty(path))
        {
            return data;
        }

        if (TryResolvePath(data, path, out object? resolved) && resolved is not null)
        {
            return resolved;
        }

        return defaultValue;
    }

    private static bool TryResolvePath(object? current, string path, out object? resolved)
    {
        resolved = null;
        if (current is null) return false;

        var segments = path.Split('.');
        object? cursor = current;

        foreach (var seg in segments)
        {
            if (cursor is null) return false;

            if (cursor is IReadOnlyDictionary<string, object?> roDict)
            {
                if (roDict.TryGetValue(seg, out var exact))
                {
                    cursor = exact;
                    continue;
                }

                bool found = false;
                foreach (var kv in roDict)
                {
                    if (string.Equals(kv.Key, seg, StringComparison.OrdinalIgnoreCase))
                    {
                        cursor = kv.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            else if (cursor is IDictionary<string, object?> dict)
            {
                if (dict.TryGetValue(seg, out var exact))
                {
                    cursor = exact;
                    continue;
                }

                bool found = false;
                foreach (var kv in dict)
                {
                    if (string.Equals(kv.Key, seg, StringComparison.OrdinalIgnoreCase))
                    {
                        cursor = kv.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            else if (cursor is IList<object?> list && int.TryParse(seg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
            {
                if (idx < 0 || idx >= list.Count) return false;
                cursor = list[idx];
            }
            else
            {
                return false;
            }
        }

        resolved = cursor;
        return true;
    }

    private static object? EvaluateAnd(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array)
        {
            return EvaluateElement(rawArgs, data, depth);
        }

        object? last = true;
        foreach (var item in rawArgs.EnumerateArray())
        {
            last = EvaluateElement(item, data, depth);
            if (!IsTruthy(last))
            {
                return last;
            }
        }

        return last;
    }

    private static object? EvaluateOr(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array)
        {
            return EvaluateElement(rawArgs, data, depth);
        }

        object? last = false;
        foreach (var item in rawArgs.EnumerateArray())
        {
            last = EvaluateElement(item, data, depth);
            if (IsTruthy(last))
            {
                return last;
            }
        }

        return last;
    }

    private static object? EvaluateIf(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array)
        {
            return EvaluateElement(rawArgs, data, depth);
        }

        var items = rawArgs.EnumerateArray().ToList();
        int i = 0;
        while (i + 1 < items.Count)
        {
            var cond = EvaluateElement(items[i], data, depth);
            if (IsTruthy(cond))
            {
                return EvaluateElement(items[i + 1], data, depth);
            }
            i += 2;
        }

        return i < items.Count ? EvaluateElement(items[i], data, depth) : null;
    }

    private enum AggregationKind { Sum, Avg }

    private static object EvaluateAggregation(
        JsonElement rawArgs,
        IReadOnlyDictionary<string, object?> data,
        int depth,
        AggregationKind kind)
    {
        // Form 1: {"sum": [{"var": "LineItems"}, "Amount"]} or {"sum": [{"var": "LineItems"}, {"*": [{"var": "Qty"}, {"var": "UnitPrice"}]}]}
        if (rawArgs.ValueKind == JsonValueKind.Array)
        {
            var items = rawArgs.EnumerateArray().ToList();
            if (items.Count == 2)
            {
                var firstEval = EvaluateElement(items[0], data, depth);
                if (firstEval is IEnumerable<object?> collection && firstEval is not string)
                {
                    var rowList = collection.ToList();
                    if (rowList.Count == 0)
                    {
                        return NormalizeDecimal(0m);
                    }

                    decimal total = 0m;
                    var selector = items[1];
                    foreach (var row in rowList)
                    {
                        object? val;
                        if (selector.ValueKind == JsonValueKind.String)
                        {
                            string colName = selector.GetString() ?? string.Empty;
                            val = TryResolvePath(row, colName, out var resolved) ? resolved : 0m;
                        }
                        else
                        {
                            var rowScope = BuildRowScope(data, row);
                            val = EvaluateElement(selector, rowScope, depth + 1);
                        }

                        total += ToDecimalOrZero(val);
                    }

                    return kind == AggregationKind.Avg
                        ? NormalizeDecimal(total / rowList.Count)
                        : NormalizeDecimal(total);
                }
            }

            // Form 2: {"sum": [1, 2, 3]} or {"sum": [{"var": "A"}, {"var": "B"}]}
            var flatNumbers = new List<decimal>();
            foreach (var item in items)
            {
                var eval = EvaluateElement(item, data, depth);
                if (eval is IEnumerable<object?> subList && eval is not string)
                {
                    foreach (var sub in subList)
                    {
                        flatNumbers.Add(ToDecimalOrZero(sub));
                    }
                }
                else
                {
                    flatNumbers.Add(ToDecimalOrZero(eval));
                }
            }

            if (flatNumbers.Count == 0) return NormalizeDecimal(0m);
            decimal sum = flatNumbers.Sum();
            return kind == AggregationKind.Avg
                ? NormalizeDecimal(sum / flatNumbers.Count)
                : NormalizeDecimal(sum);
        }

        // Form 3: {"sum": {"var": "Numbers"}}
        var single = EvaluateElement(rawArgs, data, depth);
        if (single is IEnumerable<object?> seq && single is not string)
        {
            var nums = seq.Select(ToDecimalOrZero).ToList();
            if (nums.Count == 0) return NormalizeDecimal(0m);
            decimal sum = nums.Sum();
            return kind == AggregationKind.Avg
                ? NormalizeDecimal(sum / nums.Count)
                : NormalizeDecimal(sum);
        }

        return NormalizeDecimal(ToDecimalOrZero(single));
    }

    private static object EvaluateCount(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        var args = EvaluateArgsList(rawArgs, data, depth);
        if (args.Count == 0 || args[0] is null) return 0L;
        if (args[0] is IEnumerable<object?> seq && args[0] is not string)
        {
            return (long)seq.Count();
        }
        return 1L;
    }

    private static object EvaluateMap(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array) return new List<object?>();
        var items = rawArgs.EnumerateArray().ToList();
        if (items.Count < 2) return new List<object?>();

        var source = EvaluateElement(items[0], data, depth);
        if (source is not IEnumerable<object?> seq || source is string) return new List<object?>();

        var results = new List<object?>();
        foreach (var item in seq)
        {
            var rowScope = BuildRowScope(data, item);
            results.Add(EvaluateElement(items[1], rowScope, depth + 1));
        }

        return results;
    }

    private static object EvaluateFilter(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array) return new List<object?>();
        var items = rawArgs.EnumerateArray().ToList();
        if (items.Count < 2) return new List<object?>();

        var source = EvaluateElement(items[0], data, depth);
        if (source is not IEnumerable<object?> seq || source is string) return new List<object?>();

        var results = new List<object?>();
        foreach (var item in seq)
        {
            var rowScope = BuildRowScope(data, item);
            if (IsTruthy(EvaluateElement(items[1], rowScope, depth + 1)))
            {
                results.Add(item);
            }
        }

        return results;
    }

    private static object? EvaluateReduce(JsonElement rawArgs, IReadOnlyDictionary<string, object?> data, int depth)
    {
        if (rawArgs.ValueKind != JsonValueKind.Array) return null;
        var items = rawArgs.EnumerateArray().ToList();
        if (items.Count < 3) return null;

        var source = EvaluateElement(items[0], data, depth);
        object? accumulator = EvaluateElement(items[2], data, depth);
        if (source is not IEnumerable<object?> seq || source is string) return accumulator;

        foreach (var item in seq)
        {
            var reduceScope = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["current"] = item,
                ["accumulator"] = accumulator
            };
            accumulator = EvaluateElement(items[1], reduceScope, depth + 1);
        }

        return accumulator;
    }

    private static Dictionary<string, object?> BuildRowScope(IReadOnlyDictionary<string, object?> parentData, object? row)
    {
        var scope = new Dictionary<string, object?>(parentData, StringComparer.OrdinalIgnoreCase);
        if (row is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict)
            {
                scope[kv.Key] = kv.Value;
            }
        }
        else if (row is IReadOnlyDictionary<string, object?> roDict)
        {
            foreach (var kv in roDict)
            {
                scope[kv.Key] = kv.Value;
            }
        }
        else
        {
            scope[string.Empty] = row;
        }
        return scope;
    }

    private static bool EvaluateComparison(
        List<object?> args,
        Func<decimal, decimal, bool> numCompare,
        Func<string, string, bool> strCompare)
    {
        if (args.Count < 2) return false;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (!ComparePair(args[i], args[i + 1], numCompare, strCompare))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ComparePair(
        object? left,
        object? right,
        Func<decimal, decimal, bool> numCompare,
        Func<string, string, bool> strCompare)
    {
        if (TryToDecimal(left, out decimal lNum) && TryToDecimal(right, out decimal rNum))
        {
            return numCompare(lNum, rNum);
        }

        return strCompare(ToComparableString(left), ToComparableString(right));
    }

    private static bool LooseEquals(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null)
        {
            string l = ToComparableString(left);
            string r = ToComparableString(right);
            return l.Length == 0 && r.Length == 0;
        }

        if (left is bool lb && right is bool rb) return lb == rb;
        if (TryToDecimal(left, out decimal lNum) && TryToDecimal(right, out decimal rNum))
        {
            return lNum == rNum;
        }

        return string.Equals(ToComparableString(left), ToComparableString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool StrictEquals(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        if (left is bool lb && right is bool rb) return lb == rb;
        if (IsNumericType(left) && IsNumericType(right) && TryToDecimal(left, out decimal lNum) && TryToDecimal(right, out decimal rNum))
        {
            return lNum == rNum;
        }
        if (left is string ls && right is string rs) return string.Equals(ls, rs, StringComparison.Ordinal);
        return Equals(left, right);
    }

    private static object EvaluateAdd(List<object?> args)
    {
        decimal sum = 0m;
        foreach (var arg in args)
        {
            sum += ToDecimalOrZero(arg);
        }
        return NormalizeDecimal(sum);
    }

    private static object EvaluateSubtract(List<object?> args)
    {
        if (args.Count == 0) return 0L;
        if (args.Count == 1) return NormalizeDecimal(-ToDecimalOrZero(args[0]));
        decimal result = ToDecimalOrZero(args[0]);
        for (int i = 1; i < args.Count; i++)
        {
            result -= ToDecimalOrZero(args[i]);
        }
        return NormalizeDecimal(result);
    }

    private static object EvaluateMultiply(List<object?> args)
    {
        if (args.Count == 0) return 0L;
        decimal product = 1m;
        foreach (var arg in args)
        {
            product *= ToDecimalOrZero(arg);
        }
        return NormalizeDecimal(product);
    }

    private static object EvaluateDivide(List<object?> args)
    {
        if (args.Count < 2) return 0L;
        decimal divisor = ToDecimalOrZero(args[1]);
        if (divisor == 0m) return 0L;
        return NormalizeDecimal(ToDecimalOrZero(args[0]) / divisor);
    }

    private static object EvaluateModulo(List<object?> args)
    {
        if (args.Count < 2) return 0L;
        decimal divisor = ToDecimalOrZero(args[1]);
        if (divisor == 0m) return 0L;
        return NormalizeDecimal(ToDecimalOrZero(args[0]) % divisor);
    }

    private static object EvaluateMinMax(List<object?> args, bool isMax)
    {
        if (args.Count == 0) return 0L;
        decimal best = ToDecimalOrZero(args[0]);
        for (int i = 1; i < args.Count; i++)
        {
            decimal candidate = ToDecimalOrZero(args[i]);
            if (isMax ? candidate > best : candidate < best)
            {
                best = candidate;
            }
        }
        return NormalizeDecimal(best);
    }

    private static object EvaluateRound(List<object?> args)
    {
        if (args.Count == 0) return 0L;
        decimal value = ToDecimalOrZero(args[0]);
        int decimals = args.Count > 1 ? Math.Clamp((int)ToDecimalOrZero(args[1]), 0, 10) : 0;
        return NormalizeDecimal(Math.Round(value, decimals, MidpointRounding.AwayFromZero));
    }

    private static bool EvaluateIn(List<object?> args)
    {
        if (args.Count < 2) return false;
        object? needle = args[0];
        object? haystack = args[1];

        if (haystack is string hs)
        {
            string ns = ToComparableString(needle);
            return hs.Contains(ns, StringComparison.OrdinalIgnoreCase);
        }

        if (haystack is IEnumerable<object?> seq)
        {
            return seq.Any(item => LooseEquals(item, needle));
        }

        return false;
    }

    private static bool EvaluateContains(List<object?> args)
    {
        if (args.Count < 2) return false;
        object? haystack = args[0];
        object? needle = args[1];

        if (haystack is IEnumerable<object?> seq && haystack is not string)
        {
            return seq.Any(item => LooseEquals(item, needle));
        }

        string hs = ToComparableString(haystack);
        string ns = ToComparableString(needle);
        return hs.Contains(ns, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumericType(object val) =>
        val is int or long or decimal or double or float;

    public static bool TryToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case decimal m:
                result = m;
                return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                result = (decimal)d;
                return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                result = (decimal)f;
                return true;
            case string s when !string.IsNullOrWhiteSpace(s):
                return decimal.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                result = 0m;
                return false;
        }
    }

    private static decimal ToDecimalOrZero(object? value) =>
        TryToDecimal(value, out decimal result) ? result : 0m;

    public static object NormalizeDecimal(decimal value)
    {
        decimal rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        if (rounded == decimal.Truncate(rounded) && rounded is >= long.MinValue and <= long.MaxValue)
        {
            return (long)rounded;
        }

        return rounded;
    }

    public static string ToComparableString(object? val)
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
}
