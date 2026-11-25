using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.ValueObjects;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Application.Services;

/// <summary>
/// Graph traversal and execution engine for React Flow visual BPM graphs.
/// Supports hierarchical Stage sub-workflows with pause/resume call-stacks,
/// multi-condition AND/OR rules, and infinite-cycle step limits.
/// </summary>
public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    public const int MaxExecutionSteps = 250;

    private readonly IEmailService _emailService;

    public WorkflowExecutionEngine(IEmailService emailService)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    public string? EvaluateNextNode(
        ReactFlowGraph graph,
        string currentNodeId,
        string? selectedOutcome = null,
        string? variablesJson = null)
    {
        var currentNode = graph.Nodes.FirstOrDefault(n => n.Id == currentNodeId);
        if (currentNode == null) return null;

        var outgoingEdges = graph.Edges.Where(e => e.Source == currentNodeId).ToList();
        if (outgoingEdges.Count == 0) return null;

        // 1. Decision / Condition Rule Node
        if (currentNode.Data.NodeType.Equals("decision", StringComparison.OrdinalIgnoreCase) && currentNode.Data.DecisionData != null)
        {
            bool isConditionTrue = EvaluateConditionRule(currentNode.Data.DecisionData, variablesJson);
            string targetHandle = isConditionTrue ? "decision-true" : "decision-false";

            var decisionEdge = outgoingEdges.FirstOrDefault(e => e.SourceHandle == targetHandle);
            return decisionEdge?.Target ?? outgoingEdges.FirstOrDefault()?.Target;
        }

        // 2. Approval Choice Outcome Selected (Dynamic Outcome Handle transition)
        if (!string.IsNullOrWhiteSpace(selectedOutcome))
        {
            var matchedEdge = outgoingEdges.FirstOrDefault(e =>
                string.Equals(e.SourceHandle, selectedOutcome, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.SourceHandle, $"outcome-{selectedOutcome}", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Label, selectedOutcome, StringComparison.OrdinalIgnoreCase));

            if (matchedEdge != null)
            {
                return matchedEdge.Target;
            }
        }

        // 3. Single default outgoing edge
        return outgoingEdges.FirstOrDefault()?.Target;
    }

    public Task ExecuteNodeLogicAsync(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        string nodeId,
        CancellationToken ct = default)
    {
        var stepCounter = new ExecutionStepCounter(MaxExecutionSteps);
        return ExecuteNodeInternalAsync(instance, graph, nodeId, isSubWorkflow: false, stepCounter, ct);
    }

    public async Task<string?> ResumeAfterTaskAsync(
        WorkflowInstance instance,
        ReactFlowGraph rootGraph,
        string completedNodeId,
        string selectedOutcome,
        CancellationToken ct = default)
    {
        var stepCounter = new ExecutionStepCounter(MaxExecutionSteps);
        instance.CompleteNodeExecution(completedNodeId);
        var stageStack = instance.GetStageStack();

        // Fallback discovery if ExecutionStackJson was empty on an instance paused inside a Stage
        if (stageStack.Count == 0 && rootGraph.Nodes.All(n => n.Id != completedNodeId))
        {
            if (TryFindStagePathToNode(rootGraph, completedNodeId, out var discoveredPath))
            {
                stageStack = discoveredPath;
                instance.SetStageStack(stageStack);
            }
        }

        // Case 1: Task belonged directly to the root workflow graph
        if (stageStack.Count == 0)
        {
            var nextRootNode = EvaluateNextNode(rootGraph, completedNodeId, selectedOutcome, instance.ContextVariablesJson);
            if (nextRootNode != null)
            {
                await ExecuteNodeInternalAsync(instance, rootGraph, nextRootNode, isSubWorkflow: false, stepCounter, ct);
            }
            else
            {
                instance.TransitionToNode("end-node", "end", selectedOutcome, isTerminalWorkflowEnd: true);
            }

            return instance.CurrentNodeId;
        }

        // Case 2: Task was paused inside one or more nested Stage sub-workflows.
        // Unwind completed sub-workflows up the call stack until paused again or root completes.
        string currentCompletedId = completedNodeId;
        string? pendingOutcome = selectedOutcome;

        while (stageStack.Count > 0)
        {
            var activeGraph = ResolveSubGraphByStack(rootGraph, stageStack);
            if (activeGraph == null)
            {
                // Corrupted stack fallback: clear stack and fail safely
                instance.SetStageStack(Array.Empty<string>());
                instance.RecordFailure(currentCompletedId, "stage", "INVALID_STAGE_STACK", "Unable to resolve parent stage sub-workflow graph.", null, instance.ContextVariablesJson);
                return instance.CurrentNodeId;
            }

            var nextInSub = EvaluateNextNode(activeGraph, currentCompletedId, pendingOutcome, instance.ContextVariablesJson);
            pendingOutcome = null; // Outcome applies only to the completed approval node itself

            if (nextInSub != null)
            {
                await ExecuteNodeInternalAsync(instance, activeGraph, nextInSub, isSubWorkflow: true, stepCounter, ct);

                // Refresh stack in case inner execution pushed a deeper nested Stage and paused
                if (IsExecutionPausedOrHalted(instance))
                {
                    return instance.CurrentNodeId;
                }
            }

            // Active sub-workflow finished without pausing -> pop this stage and step forward in its parent
            string? finishedStageId = instance.PopStage();
            if (finishedStageId != null)
            {
                instance.CompleteNodeExecution(finishedStageId);
            }
            stageStack = instance.GetStageStack();
            if (finishedStageId == null)
            {
                break;
            }

            currentCompletedId = finishedStageId;
        }

        // All nested stages on the stack finished -> continue in rootGraph after the outermost Stage node
        var nextAfterOutermostStage = EvaluateNextNode(rootGraph, currentCompletedId, null, instance.ContextVariablesJson);
        if (nextAfterOutermostStage != null)
        {
            await ExecuteNodeInternalAsync(instance, rootGraph, nextAfterOutermostStage, isSubWorkflow: false, stepCounter, ct);
        }
        else
        {
            instance.TransitionToNode("end-node", "end", null, isTerminalWorkflowEnd: true);
        }

        return instance.CurrentNodeId;
    }

    private async Task ExecuteNodeInternalAsync(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        string nodeId,
        bool isSubWorkflow,
        ExecutionStepCounter stepCounter,
        CancellationToken ct)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        try
        {
            stepCounter.Increment(node.Id);
        }
        catch (InvalidOperationException ex)
        {
            instance.RecordFailure(
                node.Id,
                node.Data.NodeType,
                "MAX_STEPS_EXCEEDED",
                ex.Message,
                ex.StackTrace,
                instance.ContextVariablesJson);
            return;
        }

        bool isTerminalWorkflowEnd = !isSubWorkflow;
        instance.RecordNodeStarted(node.Id, node.Data.NodeType);
        instance.TransitionToNode(node.Id, node.Data.NodeType, isTerminalWorkflowEnd: isTerminalWorkflowEnd);

        try
        {
            switch (node.Data.NodeType.ToLowerInvariant())
            {
                case "trigger":
                case "start":
                    instance.CompleteNodeExecution(node.Id);
                    var nextAfterTrigger = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterTrigger != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterTrigger, isSubWorkflow, stepCounter, ct);
                    }
                    break;

                case "approval":
                    if (node.Data.ApprovalData != null)
                    {
                        var approval = node.Data.ApprovalData;
                        var assigneeType = string.Equals(approval.AssignmentType, "group", StringComparison.OrdinalIgnoreCase)
                            ? AssigneeType.Group
                            : AssigneeType.User;

                        string taskId = $"task-{Guid.NewGuid():N}";
                        string taskTitle = !string.IsNullOrWhiteSpace(node.Data.Label) ? node.Data.Label : "Approval Task";

                        instance.CreateTask(taskId, node.Id, taskTitle, assigneeType, approval.Assignee, approval.DueDateDays);
                    }
                    instance.MarkNodeWaiting(node.Id);
                    break;

                case "sendemail":
                    if (node.Data.SendEmailData != null)
                    {
                        var email = node.Data.SendEmailData;
                        string resolvedTo = InterpolateTemplate(email.To, instance);
                        string resolvedSubject = InterpolateTemplate(email.Subject, instance);
                        string resolvedBody = InterpolateTemplate(email.Body, instance);

                        await _emailService.SendEmailAsync(resolvedTo, resolvedSubject, resolvedBody, email.IsHtml ?? false, ct);

                        instance.CompleteNodeExecution(node.Id);
                        var nextAfterEmail = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                        if (nextAfterEmail != null)
                        {
                            await ExecuteNodeInternalAsync(instance, graph, nextAfterEmail, isSubWorkflow, stepCounter, ct);
                        }
                    }
                    else
                    {
                        instance.CompleteNodeExecution(node.Id);
                    }
                    break;

                case "decision":
                    instance.CompleteNodeExecution(node.Id);
                    var nextAfterDecision = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterDecision != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterDecision, isSubWorkflow, stepCounter, ct);
                    }
                    break;

                case "stage":
                    if (node.Data.StageData?.SubWorkflow?.Nodes != null && node.Data.StageData.SubWorkflow.Nodes.Count > 0)
                    {
                        var subGraph = BuildStageSubGraph(graph, node);

                        var subTrigger = subGraph.Nodes.FirstOrDefault(n =>
                            n.Data.NodeType.Equals("trigger", StringComparison.OrdinalIgnoreCase) ||
                            n.Data.NodeType.Equals("start", StringComparison.OrdinalIgnoreCase));

                        if (subTrigger != null)
                        {
                            instance.PushStage(node.Id);
                            await ExecuteNodeInternalAsync(instance, subGraph, subTrigger.Id, isSubWorkflow: true, stepCounter, ct);

                            // CRITICAL FIX: If the sub-workflow paused on a human approval task (or failed/suspended),
                            // keep the stage on the execution stack and DO NOT advance the parent workflow!
                            if (IsExecutionPausedOrHalted(instance))
                            {
                                instance.MarkNodeWaiting(node.Id);
                                return;
                            }

                            instance.PopStage();
                        }
                    }

                    instance.CompleteNodeExecution(node.Id);
                    // Sub-workflow completed synchronously without pausing -> advance parent graph
                    var nextAfterStage = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterStage != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterStage, isSubWorkflow, stepCounter, ct);
                    }
                    break;

                case "end":
                    instance.CompleteNodeExecution(node.Id);
                    // Terminal state handled in TransitionToNode when !isSubWorkflow
                    break;
            }
        }
        catch (Exception ex)
        {
            instance.RecordFailure(
                node.Id,
                node.Data.NodeType,
                "NODE_EXECUTION_ERROR",
                ex.Message,
                ex.StackTrace,
                instance.ContextVariablesJson);
        }
    }

    private static bool IsExecutionPausedOrHalted(WorkflowInstance instance)
    {
        if (instance.Status != WorkflowStatus.Running)
        {
            return true;
        }

        if (string.Equals(instance.CurrentNodeType, "approval", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return instance.Tasks.Any(t => t.Status == TaskStatus.Pending && t.NodeId == instance.CurrentNodeId);
    }

    private static ReactFlowGraph BuildStageSubGraph(ReactFlowGraph parentGraph, ReactFlowNode stageNode)
    {
        return new ReactFlowGraph(
            $"{parentGraph.Id}-{stageNode.Id}",
            stageNode.Data.StageData?.StageName ?? stageNode.Data.Label ?? "Stage Sub-Workflow",
            parentGraph.Version,
            stageNode.Data.StageData?.SubWorkflow?.Nodes ?? new List<ReactFlowNode>(),
            stageNode.Data.StageData?.SubWorkflow?.Edges ?? new List<ReactFlowEdge>()
        );
    }

    private static ReactFlowGraph? ResolveSubGraphByStack(ReactFlowGraph rootGraph, IReadOnlyList<string> stageStack)
    {
        ReactFlowGraph currentGraph = rootGraph;
        foreach (var stageId in stageStack)
        {
            var stageNode = currentGraph.Nodes.FirstOrDefault(n =>
                n.Id == stageId &&
                n.Data.NodeType.Equals("stage", StringComparison.OrdinalIgnoreCase));

            if (stageNode?.Data.StageData?.SubWorkflow?.Nodes == null)
            {
                return null;
            }

            currentGraph = BuildStageSubGraph(currentGraph, stageNode);
        }

        return currentGraph;
    }

    private static bool TryFindStagePathToNode(ReactFlowGraph currentGraph, string targetNodeId, out List<string> stagePath)
    {
        foreach (var node in currentGraph.Nodes)
        {
            if (node.Data.NodeType.Equals("stage", StringComparison.OrdinalIgnoreCase) &&
                node.Data.StageData?.SubWorkflow?.Nodes != null)
            {
                var subGraph = BuildStageSubGraph(currentGraph, node);
                if (subGraph.Nodes.Any(n => n.Id == targetNodeId))
                {
                    stagePath = new List<string> { node.Id };
                    return true;
                }

                if (TryFindStagePathToNode(subGraph, targetNodeId, out var nestedPath))
                {
                    stagePath = new List<string> { node.Id };
                    stagePath.AddRange(nestedPath);
                    return true;
                }
            }
        }

        stagePath = new List<string>();
        return false;
    }

    public static string InterpolateTemplate(string? template, WorkflowInstance instance)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template ?? string.Empty;
        }

        JsonDocument? doc = null;
        if (!string.IsNullOrWhiteSpace(instance.ContextVariablesJson))
        {
            try
            {
                doc = JsonDocument.Parse(instance.ContextVariablesJson);
            }
            catch
            {
                doc = null;
            }
        }

        using (doc)
        {
            return Regex.Replace(template, @"\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}", match =>
            {
                string key = match.Groups[1].Value;

                if (key.Equals("instanceId", StringComparison.OrdinalIgnoreCase))
                    return instance.InstanceId;
                if (key.Equals("workflowKey", StringComparison.OrdinalIgnoreCase))
                    return instance.WorkflowKey;
                if (key.Equals("initiatorId", StringComparison.OrdinalIgnoreCase) || key.Equals("requester", StringComparison.OrdinalIgnoreCase))
                    return instance.InitiatorId;

                if (doc != null && TryResolveProperty(doc.RootElement, key, out var element))
                {
                    return element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString() ?? string.Empty,
                        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                        _ => element.ToString()
                    };
                }

                return match.Value;
            });
        }
    }

    public static bool EvaluateConditionRule(DecisionDataPayload decision, string? variablesJson)
    {
        if (decision.Conditions == null || decision.Conditions.Count == 0)
        {
            return true;
        }

        JsonDocument? doc = null;
        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            try
            {
                doc = JsonDocument.Parse(variablesJson);
            }
            catch
            {
                return false;
            }
        }

        using (doc)
        {
            bool useOr = string.Equals(decision.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase);

            if (useOr)
            {
                return decision.Conditions.Any(c => EvaluateSingleCondition(c, doc?.RootElement));
            }

            return decision.Conditions.All(c => EvaluateSingleCondition(c, doc?.RootElement));
        }
    }

    private static bool EvaluateSingleCondition(DecisionConditionDto condition, JsonElement? root)
    {
        string op = (condition.Operator ?? "equals").Trim().ToLowerInvariant();
        bool found = TryResolveProperty(root, condition.Field, out JsonElement prop);

        if (op == "is_empty")
        {
            if (!found || prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
            {
                return true;
            }

            return prop.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(prop.GetString());
        }

        if (op == "is_not_empty")
        {
            if (!found || prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
            {
                return false;
            }

            return prop.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(prop.GetString());
        }

        if (!found || prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        string expectedRaw = condition.Value ?? string.Empty;

        // 1. Numeric comparison (supports JSON numbers and numeric strings)
        if (TryGetDouble(prop, out double actualNum) &&
            double.TryParse(expectedRaw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double expectedNum))
        {
            return op switch
            {
                "greater_than" or ">" => actualNum > expectedNum,
                "greater_than_or_equal" or ">=" => actualNum >= expectedNum - 0.0001,
                "less_than" or "<" => actualNum < expectedNum,
                "less_than_or_equal" or "<=" => actualNum <= expectedNum + 0.0001,
                "equals" or "==" => Math.Abs(actualNum - expectedNum) < 0.0001,
                "not_equals" or "!=" => Math.Abs(actualNum - expectedNum) >= 0.0001,
                _ => false
            };
        }

        // 2. Boolean comparison
        if ((prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False) &&
            bool.TryParse(expectedRaw, out bool expectedBool))
        {
            bool actualBool = prop.GetBoolean();
            return op switch
            {
                "equals" or "==" => actualBool == expectedBool,
                "not_equals" or "!=" => actualBool != expectedBool,
                _ => false
            };
        }

        string actualStr = prop.ValueKind == JsonValueKind.String ? (prop.GetString() ?? string.Empty) : prop.ToString();

        // 3. ISO Date/DateTime comparison for relational operators
        if (op is "greater_than" or ">" or "greater_than_or_equal" or ">=" or "less_than" or "<" or "less_than_or_equal" or "<=")
        {
            if (DateTimeOffset.TryParse(actualStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var actualDate) &&
                DateTimeOffset.TryParse(expectedRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expectedDate))
            {
                return op switch
                {
                    "greater_than" or ">" => actualDate > expectedDate,
                    "greater_than_or_equal" or ">=" => actualDate >= expectedDate,
                    "less_than" or "<" => actualDate < expectedDate,
                    "less_than_or_equal" or "<=" => actualDate <= expectedDate,
                    _ => false
                };
            }
        }

        // 4. String comparison
        return op switch
        {
            "equals" or "==" => string.Equals(actualStr, expectedRaw, StringComparison.OrdinalIgnoreCase),
            "not_equals" or "!=" => !string.Equals(actualStr, expectedRaw, StringComparison.OrdinalIgnoreCase),
            "contains" => actualStr.Contains(expectedRaw, StringComparison.OrdinalIgnoreCase),
            "not_contains" => !actualStr.Contains(expectedRaw, StringComparison.OrdinalIgnoreCase),
            "starts_with" => actualStr.StartsWith(expectedRaw, StringComparison.OrdinalIgnoreCase),
            "ends_with" => actualStr.EndsWith(expectedRaw, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryResolveProperty(JsonElement? root, string? fieldPath, out JsonElement value)
    {
        value = default;
        if (root == null || string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        JsonElement current = root.Value;
        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (current.TryGetProperty(segment, out var exactMatch))
            {
                current = exactMatch;
                continue;
            }

            // Fallback: case-insensitive property match (e.g. "Amount" vs "amount")
            bool matchedCaseInsensitive = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (string.Equals(prop.Name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    current = prop.Value;
                    matchedCaseInsensitive = true;
                    break;
                }
            }

            if (!matchedCaseInsensitive)
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool TryGetDouble(JsonElement element, out double number)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out number))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private sealed class ExecutionStepCounter
    {
        private readonly int _maxSteps;
        private int _steps;

        public ExecutionStepCounter(int maxSteps)
        {
            _maxSteps = maxSteps;
        }

        public void Increment(string nodeId)
        {
            _steps++;
            if (_steps > _maxSteps)
            {
                throw new InvalidOperationException(
                    $"Maximum workflow execution step limit ({_maxSteps}) exceeded at node '{nodeId}'. A cyclic graph or infinite loop was detected.");
            }
        }
    }
}
