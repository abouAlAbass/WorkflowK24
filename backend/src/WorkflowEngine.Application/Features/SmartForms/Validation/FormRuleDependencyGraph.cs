using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartForms.DTOs;

namespace WorkflowEngine.Application.Features.SmartForms.Validation;

public sealed record FormRuleGraphAnalysis(
    bool HasCycle,
    IReadOnlyList<string> CyclePath,
    IReadOnlyList<string> TopologicalOrder
);

/// <summary>
/// Builds a Directed Acyclic Graph (DAG) of SmartForm field dependencies from legacy visibilityRule
/// and JSONLogic FormRule[] definitions, detects cycles, and computes topological evaluation order.
/// </summary>
public static class FormRuleDependencyGraph
{
    public static FormRuleGraphAnalysis Analyze(
        IReadOnlyList<SmartFormFieldDto> fields,
        IReadOnlyList<FormRuleDto>? rules)
    {
        // Map of canonical field names (case-insensitive -> declared name)
        var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var orderedNodes = new List<string>();
        var repeaterColumnNamesByField = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name)) continue;
            if (!canonicalNames.ContainsKey(field.Name))
            {
                canonicalNames[field.Name] = field.Name;
                orderedNodes.Add(field.Name);
            }

            if (string.Equals(field.Type, "repeater", StringComparison.OrdinalIgnoreCase) &&
                field.RepeaterColumns is { Count: > 0 })
            {
                repeaterColumnNamesByField[field.Name] = new HashSet<string>(
                    field.RepeaterColumns.Select(c => c.Name),
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        // Also register any target/source fields referenced in rules even if not yet declared in fields
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in orderedNodes)
        {
            adjacency[node] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        void EnsureNode(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!canonicalNames.ContainsKey(name))
            {
                canonicalNames[name] = name;
                orderedNodes.Add(name);
            }
            if (!adjacency.ContainsKey(name))
            {
                adjacency[name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        void AddDependency(string targetField, string sourceField)
        {
            if (string.IsNullOrWhiteSpace(targetField) || string.IsNullOrWhiteSpace(sourceField)) return;
            EnsureNode(targetField);
            EnsureNode(sourceField);
            // Edge: sourceField -> targetField (source must be evaluated before target)
            adjacency[canonicalNames[sourceField]].Add(canonicalNames[targetField]);
        }

        // 1. Legacy visibilityRule dependencies
        foreach (var field in fields)
        {
            if (field.VisibilityRule != null && !string.IsNullOrWhiteSpace(field.VisibilityRule.Field))
            {
                AddDependency(field.Name, field.VisibilityRule.Field);
            }
        }

        // 2. JSONLogic FormRule[] dependencies
        if (rules != null)
        {
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.TargetField)) continue;
                EnsureNode(rule.TargetField);

                var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ExtractVariableReferences(rule.ConditionJsonLogic, refs, repeaterColumnNamesByField);
                ExtractVariableReferences(rule.ExpressionJsonLogic, refs, repeaterColumnNamesByField);

                foreach (var source in refs)
                {
                    AddDependency(rule.TargetField, source);
                }
            }
        }

        // 3. 3-color DFS cycle detection (0 = unvisited, 1 = visiting, 2 = visited)
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in orderedNodes)
        {
            if (state.GetValueOrDefault(node, 0) != 0) continue;

            var cycle = DfsFindCycle(node, adjacency, state, parent);
            if (cycle != null)
            {
                return new FormRuleGraphAnalysis(
                    HasCycle: true,
                    CyclePath: cycle,
                    TopologicalOrder: Array.Empty<string>());
            }
        }

        // 4. Kahn's topological sort preserving field declaration order for ties
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in orderedNodes)
        {
            inDegree[node] = 0;
        }

        foreach (var kv in adjacency)
        {
            foreach (var target in kv.Value)
            {
                inDegree[target] = inDegree.GetValueOrDefault(target, 0) + 1;
            }
        }

        var ready = new Queue<string>(orderedNodes.Where(n => inDegree[n] == 0));
        var topo = new List<string>(orderedNodes.Count);

        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            topo.Add(current);

            foreach (var next in adjacency[current])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                {
                    ready.Enqueue(next);
                }
            }
        }

        return new FormRuleGraphAnalysis(
            HasCycle: false,
            CyclePath: Array.Empty<string>(),
            TopologicalOrder: topo);
    }

    public static void EnsureAcyclic(
        IReadOnlyList<SmartFormFieldDto> fields,
        IReadOnlyList<FormRuleDto>? rules)
    {
        var analysis = Analyze(fields, rules);
        if (analysis.HasCycle)
        {
            string pathText = string.Join(" -> ", analysis.CyclePath);
            throw new CyclicFormRuleException(
                $"{CyclicFormRuleException.ErrorCode}: Circular dependency detected in SmartForm rules ({pathText}).",
                analysis.CyclePath);
        }
    }

    private static List<string>? DfsFindCycle(
        string node,
        Dictionary<string, HashSet<string>> adjacency,
        Dictionary<string, int> state,
        Dictionary<string, string> parent)
    {
        state[node] = 1; // visiting

        if (adjacency.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                int neighborState = state.GetValueOrDefault(neighbor, 0);
                if (neighborState == 1)
                {
                    // Cycle found: reconstruct path neighbor -> ... -> node -> neighbor
                    var cycle = new List<string> { neighbor };
                    string curr = node;
                    while (!string.Equals(curr, neighbor, StringComparison.OrdinalIgnoreCase))
                    {
                        cycle.Add(curr);
                        if (!parent.TryGetValue(curr, out var p)) break;
                        curr = p;
                    }
                    cycle.Add(neighbor);
                    cycle.Reverse();
                    return cycle;
                }

                if (neighborState == 0)
                {
                    parent[neighbor] = node;
                    var nested = DfsFindCycle(neighbor, adjacency, state, parent);
                    if (nested != null) return nested;
                }
            }
        }

        state[node] = 2; // visited
        return null;
    }

    private static void ExtractVariableReferences(
        JsonElement? element,
        HashSet<string> refs,
        Dictionary<string, HashSet<string>> repeaterColumns,
        HashSet<string>? excludedRowColumns = null)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        var el = element.Value;
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    ExtractVariableReferences(item, refs, repeaterColumns, excludedRowColumns);
                }
                break;

            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "var", StringComparison.Ordinal))
                    {
                        string? rawPath = null;
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            rawPath = prop.Value.GetString();
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            using var arrEnum = prop.Value.EnumerateArray();
                            if (arrEnum.MoveNext() && arrEnum.Current.ValueKind == JsonValueKind.String)
                            {
                                rawPath = arrEnum.Current.GetString();
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(rawPath))
                        {
                            string rootField = rawPath.Split('.')[0].Trim();
                            if (!string.IsNullOrWhiteSpace(rootField) &&
                                (excludedRowColumns == null || !excludedRowColumns.Contains(rootField)))
                            {
                                refs.Add(rootField);
                            }
                        }
                    }
                    else if (prop.Name is "sum" or "avg" or "map" or "filter" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var items = prop.Value.EnumerateArray().ToList();
                        if (items.Count >= 1)
                        {
                            var sourceRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            ExtractVariableReferences(items[0], sourceRefs, repeaterColumns, excludedRowColumns);
                            foreach (var r in sourceRefs) refs.Add(r);

                            // Exclude columns of the iterated repeater from top-level field dependencies
                            HashSet<string>? rowCols = excludedRowColumns != null
                                ? new HashSet<string>(excludedRowColumns, StringComparer.OrdinalIgnoreCase)
                                : null;

                            foreach (var src in sourceRefs)
                            {
                                if (repeaterColumns.TryGetValue(src, out var cols))
                                {
                                    rowCols ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var c in cols) rowCols.Add(c);
                                }
                            }

                            for (int i = 1; i < items.Count; i++)
                            {
                                if (items[i].ValueKind != JsonValueKind.String)
                                {
                                    ExtractVariableReferences(items[i], refs, repeaterColumns, rowCols);
                                }
                            }
                        }
                    }
                    else
                    {
                        ExtractVariableReferences(prop.Value, refs, repeaterColumns, excludedRowColumns);
                    }
                }
                break;
        }
    }
}
