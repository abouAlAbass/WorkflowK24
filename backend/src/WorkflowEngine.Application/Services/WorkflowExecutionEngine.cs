using System.Text.Json;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services;

/// <summary>
/// Graph traversal and execution engine for React Flow visual BPM graphs.
/// </summary>
public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
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
                e.SourceHandle == selectedOutcome ||
                string.Equals(e.Label, selectedOutcome, StringComparison.OrdinalIgnoreCase));

            if (matchedEdge != null)
            {
                return matchedEdge.Target;
            }
        }

        // 3. Single default outgoing edge
        return outgoingEdges.FirstOrDefault()?.Target;
    }

    public async Task ExecuteNodeLogicAsync(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        string nodeId,
        CancellationToken ct = default)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        instance.TransitionToNode(node.Id, node.Data.NodeType);

        try
        {
            switch (node.Data.NodeType.ToLowerInvariant())
            {
                case "trigger":
                    var nextAfterTrigger = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterTrigger != null)
                    {
                        await ExecuteNodeLogicAsync(instance, graph, nextAfterTrigger, ct);
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
                    break;

                case "sendemail":
                    if (node.Data.SendEmailData != null)
                    {
                        var email = node.Data.SendEmailData;
                        await _emailService.SendEmailAsync(email.To, email.Subject, email.Body, email.IsHtml ?? false, ct);

                        var nextAfterEmail = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                        if (nextAfterEmail != null)
                        {
                            await ExecuteNodeLogicAsync(instance, graph, nextAfterEmail, ct);
                        }
                    }
                    break;

                case "decision":
                    var nextAfterDecision = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterDecision != null)
                    {
                        await ExecuteNodeLogicAsync(instance, graph, nextAfterDecision, ct);
                    }
                    break;

                case "stage":
                    // If stage contains a configured sub-workflow, execute internal sub-workflow nodes
                    if (node.Data.StageData?.SubWorkflow?.Nodes != null && node.Data.StageData.SubWorkflow.Nodes.Count > 0)
                    {
                        var subGraph = new ReactFlowGraph(
                            $"{graph.Id}-{node.Id}",
                            node.Data.StageData.StageName ?? "Stage Sub-Workflow",
                            graph.Version,
                            node.Data.StageData.SubWorkflow.Nodes,
                            node.Data.StageData.SubWorkflow.Edges ?? new List<ReactFlowEdge>()
                        );

                        var subTrigger = subGraph.Nodes.FirstOrDefault(n => 
                            n.Data.NodeType.Equals("trigger", StringComparison.OrdinalIgnoreCase) ||
                            n.Data.NodeType.Equals("start", StringComparison.OrdinalIgnoreCase));

                        if (subTrigger != null)
                        {
                            await ExecuteNodeLogicAsync(instance, subGraph, subTrigger.Id, ct);
                        }
                    }

                    // Continue graph traversal to next node in the parent workflow
                    var nextAfterStage = EvaluateNextNode(graph, node.Id, null, instance.ContextVariablesJson);
                    if (nextAfterStage != null)
                    {
                        await ExecuteNodeLogicAsync(instance, graph, nextAfterStage, ct);
                    }
                    break;

                case "end":
                    // Terminal state reached, already handled in TransitionToNode
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

    private static bool EvaluateConditionRule(DecisionDataPayload decision, string? variablesJson)
    {
        if (decision.Conditions == null || decision.Conditions.Count == 0) return true;

        var condition = decision.Conditions[0];
        if (string.IsNullOrWhiteSpace(variablesJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (doc.RootElement.TryGetProperty(condition.Field, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && double.TryParse(condition.Value, out double expectedNum))
                {
                    double actualNum = prop.GetDouble();
                    return condition.Operator switch
                    {
                        "greater_than" => actualNum > expectedNum,
                        "less_than" => actualNum < expectedNum,
                        "equals" => Math.Abs(actualNum - expectedNum) < 0.0001,
                        "not_equals" => Math.Abs(actualNum - expectedNum) >= 0.0001,
                        _ => false
                    };
                }

                string actualStr = prop.ToString();
                return condition.Operator switch
                {
                    "equals" => string.Equals(actualStr, condition.Value, StringComparison.OrdinalIgnoreCase),
                    "contains" => actualStr.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                    "not_equals" => !string.Equals(actualStr, condition.Value, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
