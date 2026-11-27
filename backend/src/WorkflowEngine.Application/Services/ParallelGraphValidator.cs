using System.Text.Json;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services;

public interface IParallelGraphValidator
{
    void ValidateGraph(string graphJson);
    void ValidateGraph(ReactFlowGraph graph);
    Dictionary<string, string> ResolveSplitJoinPairs(ReactFlowGraph graph);
}

/// <summary>
/// Performs static structural and data-conflict validation on Split/Join blocks at publish time.
/// Ensures:
/// 1. Every Split has a matching, properly nested Join (and no orphan Joins).
/// 2. No edge crosses a Split/Join block boundary (neither entering from outside nor escaping inside).
/// 3. For Join policy 'n_of_m', 1 &lt;= n &lt;= m (where m is the static branch count).
/// 4. Any variable written by multiple branches declares an explicit merge_strategy (else 422).
/// </summary>
public class ParallelGraphValidator : IParallelGraphValidator
{
    private static readonly HashSet<string> SplitNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "parallelsplit",
        "parallel_split",
        "split"
    };

    private static readonly HashSet<string> JoinNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "paralleljoin",
        "parallel_join",
        "join"
    };

    private static readonly HashSet<string> ValidSplitModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "conditional",
        "dynamic_foreach"
    };

    private static readonly HashSet<string> ValidFailurePolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "fail_instance",
        "cancel_siblings",
        "continue_without"
    };

    private static readonly HashSet<string> ValidJoinPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "any",
        "n_of_m"
    };

    private static readonly HashSet<string> ValidOnRemainingPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancel",
        "let_finish"
    };

    private static readonly HashSet<string> ValidMergeStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "error_on_conflict",
        "last_writer_wins",
        "append",
        "sum",
        "first_non_null"
    };

    private static readonly HashSet<string> ResolvingMergeStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "last_writer_wins",
        "append",
        "sum",
        "first_non_null"
    };

    public static bool IsSplitNode(ReactFlowNode node) =>
        SplitNodeTypes.Contains((node.Data?.NodeType ?? string.Empty).Trim());

    public static bool IsJoinNode(ReactFlowNode node) =>
        JoinNodeTypes.Contains((node.Data?.NodeType ?? string.Empty).Trim());

    public void ValidateGraph(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return;
        }

        ReactFlowGraph? graph;
        try
        {
            graph = JsonSerializer.Deserialize<ReactFlowGraph>(
                graphJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return;
        }

        if (graph == null)
        {
            return;
        }

        ValidateGraph(graph);
    }

    public void ValidateGraph(ReactFlowGraph graph)
    {
        var errors = new List<string>();
        ValidateGraphInternal(graph, errors);

        if (errors.Count > 0)
        {
            throw new WorkflowContractValidationException(
                $"Workflow definition failed Split/Join static validation with {errors.Count} error(s).",
                errors);
        }
    }

    public Dictionary<string, string> ResolveSplitJoinPairs(ReactFlowGraph graph)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (graph.Nodes == null || graph.Edges == null)
        {
            return pairs;
        }

        var nodeMap = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var outgoing = graph.Edges
            .GroupBy(e => e.Source, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var splitNode in graph.Nodes.Where(IsSplitNode))
        {
            if (!string.IsNullOrWhiteSpace(splitNode.Data.SplitData?.JoinNodeId) &&
                nodeMap.TryGetValue(splitNode.Data.SplitData.JoinNodeId!, out var explicitJoin) &&
                IsJoinNode(explicitJoin))
            {
                pairs[splitNode.Id] = explicitJoin.Id;
                continue;
            }

            if (outgoing.TryGetValue(splitNode.Id, out var outEdges) && outEdges.Count > 0)
            {
                var foundJoins = new HashSet<string>(StringComparer.Ordinal);
                foreach (var edge in outEdges)
                {
                    var branchResult = TraceBranchToMatchingJoin(edge.Target, nodeMap, outgoing);
                    foreach (var j in branchResult.ClosingJoins)
                    {
                        foundJoins.Add(j);
                    }
                }

                if (foundJoins.Count == 1)
                {
                    pairs[splitNode.Id] = foundJoins.First();
                }
            }
        }

        return pairs;
    }

    private void ValidateGraphInternal(ReactFlowGraph graph, List<string> errors)
    {
        if (graph.Nodes == null || graph.Nodes.Count == 0)
        {
            return;
        }

        var nodes = graph.Nodes;
        var edges = graph.Edges ?? new List<ReactFlowEdge>();

        // Validate nested Stage sub-workflows recursively
        foreach (var stageNode in nodes.Where(n =>
                     string.Equals(n.Data?.NodeType, "stage", StringComparison.OrdinalIgnoreCase) &&
                     n.Data?.StageData?.SubWorkflow?.Nodes is { Count: > 0 }))
        {
            var subGraph = new ReactFlowGraph(
                $"{graph.Id}-{stageNode.Id}",
                stageNode.Data.StageData!.StageName,
                graph.Version,
                stageNode.Data.StageData.SubWorkflow!.Nodes!,
                stageNode.Data.StageData.SubWorkflow.Edges ?? new List<ReactFlowEdge>());
            ValidateGraphInternal(subGraph, errors);
        }

        var splitNodes = nodes.Where(IsSplitNode).ToList();
        var joinNodes = nodes.Where(IsJoinNode).ToList();

        if (splitNodes.Count == 0 && joinNodes.Count == 0)
        {
            return;
        }

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var outgoing = edges
            .GroupBy(e => e.Source, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var pairedJoins = new Dictionary<string, string>(StringComparer.Ordinal); // joinId -> splitId
        var splitBlocks = new List<(ReactFlowNode Split, ReactFlowNode Join, List<BranchTraceResult> Branches, HashSet<string> InteriorNodes)>();

        foreach (var splitNode in splitNodes)
        {
            var splitData = splitNode.Data.SplitData ?? new ParallelSplitDataPayload();
            string mode = (splitData.Mode ?? "all").Trim().ToLowerInvariant();
            if (!ValidSplitModes.Contains(mode))
            {
                errors.Add($"Split node '{splitNode.Id}': invalid mode '{splitData.Mode}'. Allowed: all, conditional, dynamic_foreach.");
            }

            string onFailure = (splitData.OnBranchFailure ?? "fail_instance").Trim().ToLowerInvariant();
            if (!ValidFailurePolicies.Contains(onFailure))
            {
                errors.Add($"Split node '{splitNode.Id}': invalid onBranchFailure '{splitData.OnBranchFailure}'. Allowed: fail_instance, cancel_siblings, continue_without.");
            }

            if (mode == "dynamic_foreach" && string.IsNullOrWhiteSpace(splitData.CollectionExpression))
            {
                errors.Add($"Split node '{splitNode.Id}': mode 'dynamic_foreach' requires a non-empty 'collectionExpression'.");
            }

            if (!outgoing.TryGetValue(splitNode.Id, out var outEdges) || outEdges.Count == 0)
            {
                errors.Add($"Split node '{splitNode.Id}': must have at least one outgoing branch edge.");
                continue;
            }

            var branchResults = new List<BranchTraceResult>();
            var allClosingJoins = new HashSet<string>(StringComparer.Ordinal);
            var interiorNodes = new HashSet<string>(StringComparer.Ordinal);
            bool anyBranchEscaped = false;

            foreach (var edge in outEdges)
            {
                var trace = TraceBranchToMatchingJoin(edge.Target, nodeMap, outgoing);
                branchResults.Add(trace);

                foreach (var nId in trace.VisitedInteriorNodes)
                {
                    interiorNodes.Add(nId);
                }

                if (trace.EscapedWithoutJoin || trace.ClosingJoins.Count == 0)
                {
                    anyBranchEscaped = true;
                    errors.Add($"Split node '{splitNode.Id}': branch via edge '{edge.Id}' (target '{edge.Target}') does not converge to a matching Join node.");
                }

                foreach (var jId in trace.ClosingJoins)
                {
                    allClosingJoins.Add(jId);
                }
            }

            if (anyBranchEscaped)
            {
                continue;
            }

            if (allClosingJoins.Count != 1)
            {
                errors.Add($"Split node '{splitNode.Id}': branches converge to {allClosingJoins.Count} different Join nodes ({string.Join(", ", allClosingJoins)}). All branches of a Split must converge to a single matching Join.");
                continue;
            }

            string matchedJoinId = allClosingJoins.First();
            if (!string.IsNullOrWhiteSpace(splitData.JoinNodeId) &&
                !string.Equals(splitData.JoinNodeId.Trim(), matchedJoinId, StringComparison.Ordinal))
            {
                errors.Add($"Split node '{splitNode.Id}': configured joinNodeId '{splitData.JoinNodeId}' does not match topology Join '{matchedJoinId}'.");
            }

            if (!nodeMap.TryGetValue(matchedJoinId, out var joinNode) || !IsJoinNode(joinNode))
            {
                errors.Add($"Split node '{splitNode.Id}': matching Join '{matchedJoinId}' was not found.");
                continue;
            }

            if (pairedJoins.TryGetValue(matchedJoinId, out var otherSplitId))
            {
                errors.Add($"Join node '{matchedJoinId}' is targeted by multiple Split nodes ('{otherSplitId}' and '{splitNode.Id}'). Each Split must have its own dedicated Join.");
                continue;
            }

            pairedJoins[matchedJoinId] = splitNode.Id;
            splitBlocks.Add((splitNode, joinNode, branchResults, interiorNodes));
        }

        // Check for orphan Join nodes
        foreach (var joinNode in joinNodes)
        {
            if (!pairedJoins.ContainsKey(joinNode.Id))
            {
                errors.Add($"Join node '{joinNode.Id}' has no matching Split node.");
            }
        }

        // Validate each matched (Split, Join) block
        foreach (var (splitNode, joinNode, branches, interiorNodes) in splitBlocks)
        {
            var splitData = splitNode.Data.SplitData ?? new ParallelSplitDataPayload();
            var joinData = joinNode.Data.JoinData ?? new ParallelJoinDataPayload();
            string mode = (splitData.Mode ?? "all").Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(joinData.SplitNodeId) &&
                !string.Equals(joinData.SplitNodeId.Trim(), splitNode.Id, StringComparison.Ordinal))
            {
                errors.Add($"Join node '{joinNode.Id}': configured splitNodeId '{joinData.SplitNodeId}' does not match paired Split '{splitNode.Id}'.");
            }

            // 1. Boundary crossing check: no edge enters or exits the Split/Join block bypassing Split or Join
            foreach (var edge in edges)
            {
                bool srcInside = interiorNodes.Contains(edge.Source);
                bool dstInside = interiorNodes.Contains(edge.Target);

                if (!srcInside && edge.Source != splitNode.Id && dstInside)
                {
                    errors.Add($"Boundary violation: edge '{edge.Id}' from outside node '{edge.Source}' enters directly into interior node '{edge.Target}' of Split/Join block ('{splitNode.Id}' -> '{joinNode.Id}').");
                }

                if (srcInside && !dstInside && edge.Target != joinNode.Id)
                {
                    errors.Add($"Boundary violation: edge '{edge.Id}' exits interior node '{edge.Source}' of Split/Join block ('{splitNode.Id}' -> '{joinNode.Id}') directly to outside node '{edge.Target}'.");
                }
            }

            // 2. Join policy & n_of_m bounds validation
            string policy = (joinData.Policy ?? "all").Trim().ToLowerInvariant();
            if (!ValidJoinPolicies.Contains(policy))
            {
                errors.Add($"Join node '{joinNode.Id}': invalid policy '{joinData.Policy}'. Allowed: all, any, n_of_m.");
            }

            if (!string.IsNullOrWhiteSpace(joinData.OnRemaining) &&
                !ValidOnRemainingPolicies.Contains(joinData.OnRemaining.Trim()))
            {
                errors.Add($"Join node '{joinNode.Id}': invalid onRemaining '{joinData.OnRemaining}'. Allowed: cancel, let_finish.");
            }

            if (!string.IsNullOrWhiteSpace(joinData.MergeStrategy) &&
                !ValidMergeStrategies.Contains(joinData.MergeStrategy.Trim()))
            {
                errors.Add($"Join node '{joinNode.Id}': invalid mergeStrategy '{joinData.MergeStrategy}'. Allowed: error_on_conflict, last_writer_wins, append, sum, first_non_null.");
            }

            if (joinData.VariableMergeStrategies != null)
            {
                foreach (var kvp in joinData.VariableMergeStrategies)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Value) || !ValidMergeStrategies.Contains(kvp.Value.Trim()))
                    {
                        errors.Add($"Join node '{joinNode.Id}': invalid merge strategy '{kvp.Value}' for variable '{kvp.Key}'.");
                    }
                }
            }

            if (policy == "n_of_m")
            {
                int? n = joinData.RequiredCount ?? joinData.QuorumCount;
                int m = branches.Count;
                if (!n.HasValue || n.Value < 1)
                {
                    errors.Add($"Join node '{joinNode.Id}': policy 'n_of_m' requires a positive quorumCount/requiredCount (n >= 1).");
                }
                else if (mode != "dynamic_foreach" && n.Value > m)
                {
                    errors.Add($"Join node '{joinNode.Id}': policy 'n_of_m' has n={n.Value} which exceeds branch count m={m} from Split '{splitNode.Id}'.");
                }
            }

            // 3. Static variable write conflict detection across branches
            var variableWriters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var branch in branches)
            {
                var writtenInBranch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var nodeId in branch.VisitedInteriorNodes)
                {
                    if (!nodeMap.TryGetValue(nodeId, out var interiorNode)) continue;
                    foreach (var varName in CollectNodeWrittenVariables(interiorNode))
                    {
                        writtenInBranch.Add(varName);
                    }
                }

                foreach (var varName in writtenInBranch)
                {
                    // In dynamic_foreach, a single branch body executes N times in parallel -> treat as multi-branch write
                    int increment = mode == "dynamic_foreach" ? 2 : 1;
                    variableWriters[varName] = variableWriters.GetValueOrDefault(varName, 0) + increment;
                }
            }

            foreach (var (varName, writerCount) in variableWriters)
            {
                if (writerCount < 2) continue;

                string? effectiveStrategy = null;
                if (joinData.VariableMergeStrategies != null)
                {
                    var match = joinData.VariableMergeStrategies
                        .FirstOrDefault(kv => string.Equals(kv.Key, varName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(match.Value))
                    {
                        effectiveStrategy = match.Value.Trim();
                    }
                }

                effectiveStrategy ??= joinData.MergeStrategy?.Trim();

                if (string.IsNullOrWhiteSpace(effectiveStrategy) ||
                    !ResolvingMergeStrategies.Contains(effectiveStrategy))
                {
                    errors.Add(
                        $"Variable conflict on '{varName}' across parallel branches of Split '{splitNode.Id}' -> Join '{joinNode.Id}': " +
                        $"an explicit merge_strategy (last_writer_wins, append, sum, or first_non_null) must be declared on Join '{joinNode.Id}'.");
                }
            }
        }
    }

    private static IEnumerable<string> CollectNodeWrittenVariables(ReactFlowNode node)
    {
        if (node.Data.HttpData?.ResponseMapping != null)
        {
            foreach (var key in node.Data.HttpData.ResponseMapping.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key)) yield return key.Trim();
            }
        }

        if (node.Data.SmartObjectData?.OutputMapping != null)
        {
            foreach (var key in node.Data.SmartObjectData.OutputMapping.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key)) yield return key.Trim();
            }
        }

        if (node.Data.SetVariables != null)
        {
            foreach (var key in node.Data.SetVariables.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key)) yield return key.Trim();
            }
        }

        if (node.Data.OutputVariables != null)
        {
            foreach (var key in node.Data.OutputVariables)
            {
                if (!string.IsNullOrWhiteSpace(key)) yield return key.Trim();
            }
        }

        if (node.Data.ApprovalData?.OutputVariables != null)
        {
            foreach (var key in node.Data.ApprovalData.OutputVariables)
            {
                if (!string.IsNullOrWhiteSpace(key)) yield return key.Trim();
            }
        }
    }

    private static BranchTraceResult TraceBranchToMatchingJoin(
        string startNodeId,
        Dictionary<string, ReactFlowNode> nodeMap,
        Dictionary<string, List<ReactFlowEdge>> outgoing)
    {
        var result = new BranchTraceResult();
        var visitedState = new HashSet<(string NodeId, int Depth)>();
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            if (!visitedState.Add((currentId, depth)))
            {
                continue;
            }

            if (!nodeMap.TryGetValue(currentId, out var node))
            {
                result.EscapedWithoutJoin = true;
                continue;
            }

            if (IsJoinNode(node))
            {
                if (depth == 0)
                {
                    result.ClosingJoins.Add(currentId);
                    continue;
                }

                // Closing an inner nested Split/Join block
                result.VisitedInteriorNodes.Add(currentId);
                int nextDepth = depth - 1;
                if (!outgoing.TryGetValue(currentId, out var afterInnerJoin) || afterInnerJoin.Count == 0)
                {
                    result.EscapedWithoutJoin = true;
                    continue;
                }

                foreach (var edge in afterInnerJoin)
                {
                    queue.Enqueue((edge.Target, nextDepth));
                }
                continue;
            }

            result.VisitedInteriorNodes.Add(currentId);

            int outgoingDepth = IsSplitNode(node) ? depth + 1 : depth;
            if (!outgoing.TryGetValue(currentId, out var nextEdges) || nextEdges.Count == 0)
            {
                result.EscapedWithoutJoin = true;
                continue;
            }

            foreach (var edge in nextEdges)
            {
                queue.Enqueue((edge.Target, outgoingDepth));
            }
        }

        return result;
    }

    private sealed class BranchTraceResult
    {
        public HashSet<string> VisitedInteriorNodes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ClosingJoins { get; } = new(StringComparer.Ordinal);
        public bool EscapedWithoutJoin { get; set; }
    }
}
