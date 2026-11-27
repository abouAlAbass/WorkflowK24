using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services;

/// <summary>
/// Validates delegation rule graphs for cycles and chain depth (max 3 hops)
/// and resolves effective delegates at task creation time.
/// </summary>
public static class DelegationResolverService
{
    public const int MaxDelegationChainDepth = 3;

    public static void ValidateNewRule(DelegationRule candidate, IReadOnlyList<DelegationRule> existingActiveRules)
    {
        if (candidate.ValidTo <= candidate.ValidFrom)
        {
            throw new DelegationValidationException("Delegation rule ValidTo must be strictly after ValidFrom.");
        }

        string source = Normalize(candidate.DelegatorId);
        string target = Normalize(candidate.DelegateId);

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            throw new DelegationValidationException("DelegatorId and DelegateId are required.");
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            throw new DelegationCycleException(
                $"{DelegationCycleException.ErrorCode}: Self-delegation from '{candidate.DelegatorId}' to '{candidate.DelegateId}' creates a cycle.");
        }

        var overlappingRules = existingActiveRules
            .Where(r => r.Active &&
                        r.Id != candidate.Id &&
                        WindowsOverlap(r.ValidFrom, r.ValidTo, candidate.ValidFrom, candidate.ValidTo) &&
                        ScopesOverlap(r.Scope, candidate.Scope))
            .ToList();

        var forward = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var reverse = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        void AddEdge(string from, string to)
        {
            if (!forward.TryGetValue(from, out var fwdSet))
            {
                fwdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                forward[from] = fwdSet;
            }
            fwdSet.Add(to);

            if (!reverse.TryGetValue(to, out var revSet))
            {
                revSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                reverse[to] = revSet;
            }
            revSet.Add(from);
        }

        foreach (var rule in overlappingRules)
        {
            AddEdge(Normalize(rule.DelegatorId), Normalize(rule.DelegateId));
        }

        AddEdge(source, target);

        // 1. Cycle detection: can we reach `source` starting from `target`?
        if (IsReachable(forward, target, source))
        {
            throw new DelegationCycleException(
                $"{DelegationCycleException.ErrorCode}: Creating delegation rule '{candidate.DelegatorId}' -> '{candidate.DelegateId}' introduces a circular delegation chain.");
        }

        // 2. Max chain depth check (max 3 hops across the graph passing through source -> target)
        int upstreamDepth = ComputeLongestPath(reverse, source, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        int downstreamDepth = ComputeLongestPath(forward, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        int totalChainDepth = upstreamDepth + 1 + downstreamDepth;

        if (totalChainDepth > MaxDelegationChainDepth)
        {
            throw new DelegationChainTooDeepException(
                $"{DelegationChainTooDeepException.ErrorCode}: Delegation chain depth ({totalChainDepth}) exceeds the maximum allowed limit of {MaxDelegationChainDepth} levels.");
        }
    }

    public static string? ResolveEffectiveDelegate(
        string originalAssigneeId,
        string? workflowKey,
        DateTime utcNow,
        IReadOnlyList<DelegationRule> activeRules)
    {
        if (string.IsNullOrWhiteSpace(originalAssigneeId) || activeRules.Count == 0)
        {
            return null;
        }

        var effectiveRules = activeRules
            .Where(r => r.IsEffectiveAt(utcNow, workflowKey))
            .ToList();

        if (effectiveRules.Count == 0)
        {
            return null;
        }

        string current = originalAssigneeId.Trim();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current };

        for (int hop = 0; hop < MaxDelegationChainDepth; hop++)
        {
            // Prefer exact workflow scope over wildcard "*"
            var matchingRule = effectiveRules
                .Where(r => string.Equals(r.DelegatorId, current, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => !string.Equals(r.Scope, "*", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (matchingRule == null)
            {
                break;
            }

            string next = matchingRule.DelegateId.Trim();
            if (!visited.Add(next))
            {
                break;
            }

            current = next;
        }

        return string.Equals(current, originalAssigneeId.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : current;
    }

    private static bool WindowsOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        return aStart < bEnd && bStart < aEnd;
    }

    private static bool ScopesOverlap(string? a, string? b)
    {
        string scopeA = string.IsNullOrWhiteSpace(a) ? "*" : a.Trim();
        string scopeB = string.IsNullOrWhiteSpace(b) ? "*" : b.Trim();

        return string.Equals(scopeA, "*", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scopeB, "*", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scopeA, scopeB, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReachable(Dictionary<string, HashSet<string>> graph, string start, string goal)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (string.Equals(node, goal, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (graph.TryGetValue(node, out var neighbors))
            {
                foreach (var next in neighbors)
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return false;
    }

    private static int ComputeLongestPath(
        Dictionary<string, HashSet<string>> graph,
        string node,
        HashSet<string> visiting)
    {
        if (!graph.TryGetValue(node, out var neighbors) || neighbors.Count == 0)
        {
            return 0;
        }

        visiting.Add(node);
        int maxDepth = 0;

        foreach (var next in neighbors)
        {
            if (visiting.Contains(next))
            {
                continue;
            }

            int depth = 1 + ComputeLongestPath(graph, next, visiting);
            if (depth > maxDepth)
            {
                maxDepth = depth;
            }
        }

        visiting.Remove(node);
        return maxDepth;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim();
}
