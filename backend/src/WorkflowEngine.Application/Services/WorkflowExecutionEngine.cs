using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Executors;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
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
    private readonly INodeExecutorRegistry? _nodeExecutorRegistry;
    private readonly IDelegationRuleRepository? _delegationRuleRepository;
    private readonly IParallelGraphValidator _parallelGraphValidator;

    public WorkflowExecutionEngine(
        IEmailService emailService,
        INodeExecutorRegistry? nodeExecutorRegistry = null,
        IDelegationRuleRepository? delegationRuleRepository = null,
        IParallelGraphValidator? parallelGraphValidator = null)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _nodeExecutorRegistry = nodeExecutorRegistry;
        _delegationRuleRepository = delegationRuleRepository;
        _parallelGraphValidator = parallelGraphValidator ?? new ParallelGraphValidator();
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
        CancellationToken ct = default,
        string? tokenId = null)
    {
        var stepCounter = new ExecutionStepCounter(MaxExecutionSteps);
        instance.CompleteNodeExecution(completedNodeId);

        // Parallel token resumption path
        if (!string.IsNullOrWhiteSpace(tokenId))
        {
            var activeToken = instance.Tokens.FirstOrDefault(t =>
                string.Equals(t.Id, tokenId, StringComparison.Ordinal));

            if (activeToken != null)
            {
                if (!activeToken.IsOpen)
                {
                    return instance.CurrentNodeId;
                }

                string? effectiveVars = BuildEffectiveVariablesJson(instance.ContextVariablesJson, activeToken.BranchVariablesJson);
                var nextNodeForToken = EvaluateNextNode(rootGraph, completedNodeId, selectedOutcome, effectiveVars);
                if (nextNodeForToken != null)
                {
                    await ExecuteNodeInternalAsync(instance, rootGraph, nextNodeForToken, isSubWorkflow: false, stepCounter, ct, activeToken);
                }
                else
                {
                    activeToken.MarkCompleted(completedNodeId);
                }

                return instance.CurrentNodeId;
            }
        }

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
        CancellationToken ct,
        WorkflowToken? activeToken = null)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        if (activeToken != null && !activeToken.IsOpen)
        {
            return;
        }

        try
        {
            stepCounter.Increment(node.Id);
        }
        catch (InvalidOperationException ex)
        {
            HandleNodeExecutionFailure(
                instance,
                graph,
                node,
                "MAX_STEPS_EXCEEDED",
                ex.Message,
                ex.StackTrace,
                activeToken);
            return;
        }

        activeToken?.MoveToNode(node.Id);

        bool isTerminalWorkflowEnd = !isSubWorkflow && activeToken == null;
        instance.RecordNodeStarted(node.Id, node.Data.NodeType);
        instance.TransitionToNode(node.Id, node.Data.NodeType, isTerminalWorkflowEnd: isTerminalWorkflowEnd);

        // Apply optional declarative node variable writes (e.g. setVariables)
        if (node.Data.SetVariables is { Count: > 0 })
        {
            ApplySetVariables(instance, node.Data.SetVariables, activeToken);
        }

        string? effectiveVarsJson = BuildEffectiveVariablesJson(instance.ContextVariablesJson, activeToken?.BranchVariablesJson);

        try
        {
            switch (node.Data.NodeType.ToLowerInvariant())
            {
                case "trigger":
                case "start":
                    instance.CompleteNodeExecution(node.Id);
                    var nextAfterTrigger = EvaluateNextNode(graph, node.Id, null, effectiveVarsJson);
                    if (nextAfterTrigger != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterTrigger, isSubWorkflow, stepCounter, ct, activeToken);
                    }
                    break;

                case "parallelsplit":
                case "parallel_split":
                case "split":
                    await ExecuteParallelSplitAsync(instance, graph, node, isSubWorkflow, stepCounter, ct, activeToken, effectiveVarsJson);
                    break;

                case "paralleljoin":
                case "parallel_join":
                case "join":
                    await ExecuteParallelJoinAsync(instance, graph, node, isSubWorkflow, stepCounter, ct, activeToken);
                    break;

                case "approval":
                    if (node.Data.ApprovalData != null)
                    {
                        var approval = node.Data.ApprovalData;
                        var assigneeType = approval.AssignmentType?.Trim().ToLowerInvariant() switch
                        {
                            "group" => AssigneeType.Group,
                            "role" => AssigneeType.Role,
                            _ => AssigneeType.User
                        };

                        var resolutionPolicy = approval.ResolutionPolicy?.Trim().ToLowerInvariant() switch
                        {
                            "claim" => TaskResolutionPolicy.Claim,
                            "consensus" => TaskResolutionPolicy.Consensus,
                            "quorum" => TaskResolutionPolicy.Quorum,
                            _ => TaskResolutionPolicy.Single
                        };

                        string taskId = $"task-{Guid.NewGuid():N}";
                        string rawTitle = !string.IsNullOrWhiteSpace(node.Data.Label) ? node.Data.Label : "Approval Task";
                        string taskTitle = InterpolateTemplateWithVars(rawTitle, instance, effectiveVarsJson);
                        string resolvedAssignee = InterpolateTemplateWithVars(approval.Assignee, instance, effectiveVarsJson);

                        var createdTask = instance.CreateTask(
                            taskId,
                            node.Id,
                            taskTitle,
                            assigneeType,
                            resolvedAssignee,
                            approval.DueDateDays,
                            resolutionPolicy,
                            approval.QuorumCount,
                            approval.DueAt,
                            activeToken?.Id,
                            activeToken?.BranchId);

                        // Apply active Out-of-Office delegation rules automatically for User tasks
                        if (assigneeType == AssigneeType.User && _delegationRuleRepository != null)
                        {
                            var activeRules = await _delegationRuleRepository.GetActiveRulesAsync(ct);
                            string? resolvedDelegate = DelegationResolverService.ResolveEffectiveDelegate(
                                resolvedAssignee,
                                instance.WorkflowKey,
                                DateTime.UtcNow,
                                activeRules);

                            if (!string.IsNullOrWhiteSpace(resolvedDelegate))
                            {
                                createdTask.ApplyAutoDelegation(
                                    resolvedDelegate,
                                    $"Auto-delegated from '{resolvedAssignee}' via active Out-of-Office rule");

                                instance.AddEvent(
                                    "Task.AutoDelegated",
                                    node.Id,
                                    "system",
                                    "system",
                                    JsonSerializer.Serialize(new
                                    {
                                        taskId = createdTask.TaskId,
                                        fromAssigneeId = resolvedAssignee,
                                        toAssigneeId = resolvedDelegate,
                                        workflowKey = instance.WorkflowKey
                                    }));
                            }
                        }
                    }
                    activeToken?.MarkWaiting(node.Id);
                    instance.MarkNodeWaiting(node.Id);
                    break;

                case "sendemail":
                    if (node.Data.SendEmailData != null)
                    {
                        var email = node.Data.SendEmailData;
                        string resolvedTo = InterpolateTemplateWithVars(email.To, instance, effectiveVarsJson);
                        string resolvedSubject = InterpolateTemplateWithVars(email.Subject, instance, effectiveVarsJson);
                        string resolvedBody = InterpolateTemplateWithVars(email.Body, instance, effectiveVarsJson);

                        await _emailService.SendEmailAsync(resolvedTo, resolvedSubject, resolvedBody, email.IsHtml ?? false, ct);

                        instance.CompleteNodeExecution(node.Id);
                        var nextAfterEmail = EvaluateNextNode(graph, node.Id, null, effectiveVarsJson);
                        if (nextAfterEmail != null)
                        {
                            await ExecuteNodeInternalAsync(instance, graph, nextAfterEmail, isSubWorkflow, stepCounter, ct, activeToken);
                        }
                    }
                    else
                    {
                        instance.CompleteNodeExecution(node.Id);
                        var nextAfterEmail = EvaluateNextNode(graph, node.Id, null, effectiveVarsJson);
                        if (nextAfterEmail != null)
                        {
                            await ExecuteNodeInternalAsync(instance, graph, nextAfterEmail, isSubWorkflow, stepCounter, ct, activeToken);
                        }
                    }
                    break;

                case "decision":
                    instance.CompleteNodeExecution(node.Id);
                    var nextAfterDecision = EvaluateNextNode(graph, node.Id, null, effectiveVarsJson);
                    if (nextAfterDecision != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterDecision, isSubWorkflow, stepCounter, ct, activeToken);
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
                            await ExecuteNodeInternalAsync(instance, subGraph, subTrigger.Id, isSubWorkflow: true, stepCounter, ct, activeToken);

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
                    var nextAfterStage = EvaluateNextNode(graph, node.Id, null, effectiveVarsJson);
                    if (nextAfterStage != null)
                    {
                        await ExecuteNodeInternalAsync(instance, graph, nextAfterStage, isSubWorkflow, stepCounter, ct, activeToken);
                    }
                    break;

                case "end":
                    instance.CompleteNodeExecution(node.Id);
                    activeToken?.MarkCompleted(node.Id);
                    // Terminal state handled in TransitionToNode when !isSubWorkflow && activeToken == null
                    break;

                default:
                    var pluggableExecutor = _nodeExecutorRegistry?.Resolve(node.Data.NodeType);
                    if (pluggableExecutor != null)
                    {
                        string? originalInstanceVars = instance.ContextVariablesJson;
                        if (activeToken != null && !string.IsNullOrWhiteSpace(effectiveVarsJson))
                        {
                            // Temporarily present merged branch variables to pluggable executor
                            instance.UpdateVariables(effectiveVarsJson);
                        }

                        var execContext = new NodeExecutionContext(instance, graph, node, isSubWorkflow);
                        var execResult = await pluggableExecutor.ExecuteAsync(execContext, ct);

                        if (activeToken != null)
                        {
                            CaptureBranchVariableDiff(instance, activeToken, originalInstanceVars, effectiveVarsJson);
                        }

                        switch (execResult.Outcome)
                        {
                            case NodeExecutionOutcome.Completed:
                                instance.CompleteNodeExecution(node.Id);
                                string? refreshedVars = BuildEffectiveVariablesJson(instance.ContextVariablesJson, activeToken?.BranchVariablesJson);
                                string? nextNode = execResult.ExplicitNextNodeId
                                    ?? EvaluateNextNode(graph, node.Id, execResult.SelectedOutcome, refreshedVars);
                                if (nextNode != null)
                                {
                                    await ExecuteNodeInternalAsync(instance, graph, nextNode, isSubWorkflow, stepCounter, ct, activeToken);
                                }
                                break;

                            case NodeExecutionOutcome.Waiting:
                                activeToken?.MarkWaiting(node.Id);
                                instance.MarkNodeWaiting(node.Id);
                                break;

                            case NodeExecutionOutcome.Faulted:
                                HandleNodeExecutionFailure(
                                    instance,
                                    graph,
                                    node,
                                    execResult.ErrorCode ?? "NODE_EXECUTION_ERROR",
                                    execResult.ErrorMessage ?? "Pluggable node executor faulted.",
                                    null,
                                    activeToken);
                                break;
                        }
                    }
                    break;
            }
        }
        catch (UserCredentialRequiredException credEx)
        {
            HandleWaitingForCredentials(instance, node, credEx, activeToken);
        }
        catch (Exception ex)
        {
            HandleNodeExecutionFailure(
                instance,
                graph,
                node,
                "NODE_EXECUTION_ERROR",
                ex.Message,
                ex.StackTrace,
                activeToken);
        }
    }

    private static void HandleWaitingForCredentials(
        WorkflowInstance instance,
        ReactFlowNode node,
        UserCredentialRequiredException credEx,
        WorkflowToken? activeToken)
    {
        if (instance.Status == WorkflowStatus.Running)
        {
            instance.Wait();
        }

        activeToken?.MarkWaiting(node.Id);
        instance.MarkNodeWaiting(node.Id);

        string taskId = $"task-{Guid.NewGuid():N}";
        string metadataJson = JsonSerializer.Serialize(new
        {
            type = "user_credential_required",
            requiredConnectionId = credEx.ConnectionId,
            connectionName = credEx.ConnectionName
        });

        instance.CreateTask(
            taskId,
            node.Id,
            $"Renseignez vos identifiants pour {credEx.ConnectionName}",
            AssigneeType.User,
            credEx.UserId,
            dueDateDays: null,
            resolutionPolicy: TaskResolutionPolicy.Single,
            quorumCount: null,
            explicitDueAt: null,
            tokenId: activeToken?.Id,
            branchId: activeToken?.BranchId,
            metadataJson: metadataJson);

        instance.AddEvent(
            "Instance.WaitingForCredentials",
            node.Id,
            credEx.UserId,
            "system",
            JsonSerializer.Serialize(new
            {
                taskId,
                requiredConnectionId = credEx.ConnectionId,
                connectionName = credEx.ConnectionName,
                userId = credEx.UserId
            }));
    }

    private async Task ExecuteParallelSplitAsync(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        ReactFlowNode splitNode,
        bool isSubWorkflow,
        ExecutionStepCounter stepCounter,
        CancellationToken ct,
        WorkflowToken? parentToken,
        string? effectiveVarsJson)
    {
        var splitData = splitNode.Data.SplitData ?? new ParallelSplitDataPayload();
        string mode = (splitData.Mode ?? "all").Trim().ToLowerInvariant();

        var outgoingEdges = graph.Edges.Where(e => e.Source == splitNode.Id).ToList();
        var pairs = _parallelGraphValidator.ResolveSplitJoinPairs(graph);
        string? joinNodeId = !string.IsNullOrWhiteSpace(splitData.JoinNodeId)
            ? splitData.JoinNodeId.Trim()
            : pairs.GetValueOrDefault(splitNode.Id);

        if (string.IsNullOrWhiteSpace(joinNodeId))
        {
            instance.RecordFailure(
                splitNode.Id,
                splitNode.Data.NodeType,
                "PARALLEL_SPLIT_NO_JOIN",
                $"Split node '{splitNode.Id}' has no matching Join node.",
                null,
                instance.ContextVariablesJson);
            return;
        }

        var branches = new List<(string BranchId, string TargetNodeId, string? InitialBranchVarsJson)>();

        if (mode == "dynamic_foreach")
        {
            var items = ResolveCollectionItems(splitData.CollectionExpression, effectiveVarsJson);
            string itemVarName = string.IsNullOrWhiteSpace(splitData.ItemVariable) ? "item" : splitData.ItemVariable.Trim();
            string? targetNodeId = outgoingEdges.FirstOrDefault()?.Target;

            if (!string.IsNullOrWhiteSpace(targetNodeId))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var branchVars = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [itemVarName] = items[i],
                        ["itemIndex"] = i
                    };
                    branches.Add(($"item-{i}", targetNodeId, JsonSerializer.Serialize(branchVars)));
                }
            }
        }
        else if (mode == "conditional")
        {
            for (int i = 0; i < outgoingEdges.Count; i++)
            {
                var edge = outgoingEdges[i];
                DecisionDataPayload? condition = null;
                if (splitData.BranchConditions != null)
                {
                    if (!splitData.BranchConditions.TryGetValue(edge.Id, out condition) &&
                        (string.IsNullOrWhiteSpace(edge.SourceHandle) || !splitData.BranchConditions.TryGetValue(edge.SourceHandle, out condition)) &&
                        (string.IsNullOrWhiteSpace(edge.Label) || !splitData.BranchConditions.TryGetValue(edge.Label, out condition)))
                    {
                        splitData.BranchConditions.TryGetValue(edge.Target, out condition);
                    }
                }

                if (condition == null || EvaluateConditionRule(condition, effectiveVarsJson))
                {
                    string branchId = !string.IsNullOrWhiteSpace(edge.SourceHandle)
                        ? edge.SourceHandle!
                        : (!string.IsNullOrWhiteSpace(edge.Id) ? edge.Id : $"branch-{i}");
                    branches.Add((branchId, edge.Target, null));
                }
            }
        }
        else // "all"
        {
            for (int i = 0; i < outgoingEdges.Count; i++)
            {
                var edge = outgoingEdges[i];
                string branchId = !string.IsNullOrWhiteSpace(edge.SourceHandle)
                    ? edge.SourceHandle!
                    : (!string.IsNullOrWhiteSpace(edge.Id) ? edge.Id : $"branch-{i}");
                branches.Add((branchId, edge.Target, null));
            }
        }

        instance.CompleteNodeExecution(splitNode.Id);

        if (branches.Count == 0)
        {
            var emptyJoin = instance.AddJoinState($"join-{Guid.NewGuid():N}", splitNode.Id, joinNodeId, 0);
            emptyJoin.TryMarkFired();
            instance.AddEvent(
                "Parallel.SplitEmpty",
                splitNode.Id,
                "system",
                "system",
                JsonSerializer.Serialize(new { splitNodeId = splitNode.Id, joinNodeId, mode }));

            instance.CompleteNodeExecution(joinNodeId);
            string? nextAfterEmptyJoin = EvaluateNextNode(graph, joinNodeId, null, effectiveVarsJson);
            if (nextAfterEmptyJoin != null)
            {
                await ExecuteNodeInternalAsync(instance, graph, nextAfterEmptyJoin, isSubWorkflow, stepCounter, ct, parentToken);
            }
            return;
        }

        var joinState = instance.AddJoinState($"join-{Guid.NewGuid():N}", splitNode.Id, joinNodeId, branches.Count);
        var joinNode = graph.Nodes.FirstOrDefault(n => n.Id == joinNodeId);
        string onRemaining = (joinNode?.Data.JoinData?.OnRemaining ?? "cancel").Trim().ToLowerInvariant();

        instance.AddEvent(
            "Parallel.SplitStarted",
            splitNode.Id,
            "system",
            "system",
            JsonSerializer.Serialize(new
            {
                splitNodeId = splitNode.Id,
                joinNodeId,
                joinStateId = joinState.Id,
                mode,
                expectedCount = branches.Count,
                branches = branches.Select(b => b.BranchId).ToList()
            }));

        // Spawn all tokens upfront so sibling cancellation works cleanly even for synchronous branches
        var spawned = new List<(WorkflowToken Token, string TargetNodeId)>(branches.Count);
        foreach (var branch in branches)
        {
            string tokenId = $"tok-{Guid.NewGuid():N}";
            var token = instance.AddToken(
                tokenId,
                branch.TargetNodeId,
                parentToken?.Id,
                branch.BranchId,
                splitNode.Id,
                branch.InitialBranchVarsJson);
            spawned.Add((token, branch.TargetNodeId));
        }

        foreach (var (token, targetNodeId) in spawned)
        {
            if (instance.Status == WorkflowStatus.Faulted || instance.Status == WorkflowStatus.Cancelled)
            {
                break;
            }

            if (joinState.JoinFired && onRemaining == "cancel")
            {
                token.MarkCancelled();
                continue;
            }

            if (!token.IsOpen)
            {
                continue;
            }

            await ExecuteNodeInternalAsync(instance, graph, targetNodeId, isSubWorkflow, stepCounter, ct, token);
        }
    }

    private async Task ExecuteParallelJoinAsync(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        ReactFlowNode joinNode,
        bool isSubWorkflow,
        ExecutionStepCounter stepCounter,
        CancellationToken ct,
        WorkflowToken? activeToken)
    {
        if (activeToken == null)
        {
            instance.CompleteNodeExecution(joinNode.Id);
            var next = EvaluateNextNode(graph, joinNode.Id, null, instance.ContextVariablesJson);
            if (next != null)
            {
                await ExecuteNodeInternalAsync(instance, graph, next, isSubWorkflow, stepCounter, ct, null);
            }
            return;
        }

        var joinData = joinNode.Data.JoinData ?? new ParallelJoinDataPayload();
        string policy = (joinData.Policy ?? "all").Trim().ToLowerInvariant();
        string onRemaining = (joinData.OnRemaining ?? "cancel").Trim().ToLowerInvariant();

        var joinState = instance.JoinStates
            .Where(j => string.Equals(j.JoinNodeId, joinNode.Id, StringComparison.Ordinal) &&
                        (string.IsNullOrWhiteSpace(activeToken.SplitNodeId) ||
                         string.Equals(j.SplitNodeId, activeToken.SplitNodeId, StringComparison.Ordinal)))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefault();

        if (joinState == null)
        {
            activeToken.MarkCompleted(joinNode.Id);
            instance.CompleteNodeExecution(joinNode.Id);
            return;
        }

        activeToken.MarkCompleted(joinNode.Id);
        joinState.RecordArrival();

        instance.AddEvent(
            "Parallel.TokenArrived",
            joinNode.Id,
            "system",
            "system",
            JsonSerializer.Serialize(new
            {
                tokenId = activeToken.Id,
                branchId = activeToken.BranchId,
                splitNodeId = joinState.SplitNodeId,
                joinNodeId = joinNode.Id,
                arrivedCount = joinState.ArrivedCount,
                expectedCount = joinState.ExpectedCount,
                joinFired = joinState.JoinFired
            }));

        int requiredArrivals = ComputeRequiredJoinArrivals(instance, joinState, joinData, policy);
        bool thresholdMet = joinState.ArrivedCount >= requiredArrivals;

        if (!thresholdMet || !joinState.TryMarkFired())
        {
            if (!joinState.JoinFired)
            {
                instance.MarkNodeWaiting(joinNode.Id);
            }
            return;
        }

        // Join fired! Cancel remaining sibling tokens & their open tasks if policy requests it
        if (onRemaining == "cancel" && (policy is "any" or "n_of_m"))
        {
            CancelSiblingTokensAndTasks(instance, joinState.SplitNodeId, activeToken.Id, joinNode.Id);
        }

        // Deterministic variable merge across completed tokens of this Split
        var completedTokens = instance.Tokens
            .Where(t => string.Equals(t.SplitNodeId, joinState.SplitNodeId, StringComparison.Ordinal) &&
                        t.Status == TokenStatus.Completed)
            .ToList();

        var parentToken = !string.IsNullOrWhiteSpace(activeToken.ParentTokenId)
            ? instance.Tokens.FirstOrDefault(t => string.Equals(t.Id, activeToken.ParentTokenId, StringComparison.Ordinal))
            : null;

        string? baseVarsJson = parentToken != null
            ? parentToken.BranchVariablesJson
            : instance.ContextVariablesJson;

        var mergeResult = MergeBranchVariables(baseVarsJson, completedTokens, joinData);
        if (!mergeResult.Success)
        {
            instance.RecordFailure(
                joinNode.Id,
                joinNode.Data.NodeType,
                "PARALLEL_MERGE_CONFLICT",
                mergeResult.ErrorMessage ?? "Unresolved variable conflict during parallel Join merge.",
                null,
                instance.ContextVariablesJson);
            return;
        }

        if (mergeResult.MergedJson != null)
        {
            if (parentToken != null)
            {
                parentToken.UpdateBranchVariables(mergeResult.MergedJson);
            }
            else
            {
                instance.UpdateVariables(mergeResult.MergedJson);
            }
        }

        instance.CompleteNodeExecution(joinNode.Id);
        instance.AddEvent(
            "Parallel.JoinFired",
            joinNode.Id,
            "system",
            "system",
            JsonSerializer.Serialize(new
            {
                splitNodeId = joinState.SplitNodeId,
                joinNodeId = joinNode.Id,
                joinStateId = joinState.Id,
                policy,
                arrivedCount = joinState.ArrivedCount,
                expectedCount = joinState.ExpectedCount,
                mergedKeys = mergeResult.MergedKeys
            }));

        string? downstreamVars = BuildEffectiveVariablesJson(instance.ContextVariablesJson, parentToken?.BranchVariablesJson);
        string? nextAfterJoin = EvaluateNextNode(graph, joinNode.Id, null, downstreamVars);
        if (nextAfterJoin != null)
        {
            await ExecuteNodeInternalAsync(instance, graph, nextAfterJoin, isSubWorkflow, stepCounter, ct, parentToken);
        }
        else if (parentToken == null && !isSubWorkflow)
        {
            instance.TransitionToNode("end-node", "end", null, isTerminalWorkflowEnd: true);
        }
    }

    private static int ComputeRequiredJoinArrivals(
        WorkflowInstance instance,
        JoinState joinState,
        ParallelJoinDataPayload joinData,
        string policy)
    {
        if (policy == "any")
        {
            return 1;
        }

        if (policy == "n_of_m")
        {
            int n = joinData.RequiredCount ?? joinData.QuorumCount ?? joinState.ExpectedCount;
            return Math.Clamp(n, 1, Math.Max(1, joinState.ExpectedCount));
        }

        // policy == "all": account for faulted branches when split uses on_branch_failure = continue_without
        int faultedCount = instance.Tokens.Count(t =>
            string.Equals(t.SplitNodeId, joinState.SplitNodeId, StringComparison.Ordinal) &&
            t.Status == TokenStatus.Faulted);

        return Math.Max(1, joinState.ExpectedCount - faultedCount);
    }

    private static void CancelSiblingTokensAndTasks(
        WorkflowInstance instance,
        string splitNodeId,
        string? excludeTokenId,
        string triggerNodeId)
    {
        var cancelledTokenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sibling in instance.Tokens.Where(t =>
                     string.Equals(t.SplitNodeId, splitNodeId, StringComparison.Ordinal) &&
                     !string.Equals(t.Id, excludeTokenId, StringComparison.Ordinal) &&
                     t.IsOpen))
        {
            sibling.MarkCancelled();
            cancelledTokenIds.Add(sibling.Id);
        }

        if (cancelledTokenIds.Count == 0)
        {
            return;
        }

        var cancelledTaskIds = new List<string>();
        foreach (var task in instance.Tasks.Where(t =>
                     t.IsActive &&
                     !string.IsNullOrWhiteSpace(t.TokenId) &&
                     cancelledTokenIds.Contains(t.TokenId)))
        {
            task.Cancel();
            cancelledTaskIds.Add(task.TaskId);
        }

        instance.AddEvent(
            "Parallel.SiblingsCancelled",
            triggerNodeId,
            "system",
            "system",
            JsonSerializer.Serialize(new
            {
                splitNodeId,
                cancelledTokens = cancelledTokenIds.ToList(),
                cancelledTasks = cancelledTaskIds
            }));
    }

    private void HandleNodeExecutionFailure(
        WorkflowInstance instance,
        ReactFlowGraph graph,
        ReactFlowNode node,
        string errorCode,
        string errorMessage,
        string? stackTrace,
        WorkflowToken? activeToken)
    {
        if (activeToken == null)
        {
            instance.RecordFailure(node.Id, node.Data.NodeType, errorCode, errorMessage, stackTrace, instance.ContextVariablesJson);
            return;
        }

        activeToken.MarkFaulted(node.Id);
        var splitNode = !string.IsNullOrWhiteSpace(activeToken.SplitNodeId)
            ? graph.Nodes.FirstOrDefault(n => string.Equals(n.Id, activeToken.SplitNodeId, StringComparison.Ordinal))
            : null;

        string onFailure = (splitNode?.Data.SplitData?.OnBranchFailure ?? "fail_instance").Trim().ToLowerInvariant();

        instance.AddEvent(
            "Parallel.BranchFailed",
            node.Id,
            "system",
            "system",
            JsonSerializer.Serialize(new
            {
                tokenId = activeToken.Id,
                branchId = activeToken.BranchId,
                splitNodeId = activeToken.SplitNodeId,
                policy = onFailure,
                errorCode,
                errorMessage
            }));

        if (onFailure == "continue_without" && !string.IsNullOrWhiteSpace(activeToken.SplitNodeId))
        {
            var joinState = instance.JoinStates
                .Where(j => string.Equals(j.SplitNodeId, activeToken.SplitNodeId, StringComparison.Ordinal))
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefault();

            int nonFaultedCount = instance.Tokens.Count(t =>
                string.Equals(t.SplitNodeId, activeToken.SplitNodeId, StringComparison.Ordinal) &&
                t.Status != TokenStatus.Faulted);

            if (joinState != null && nonFaultedCount > 0)
            {
                var joinNode = graph.Nodes.FirstOrDefault(n => string.Equals(n.Id, joinState.JoinNodeId, StringComparison.Ordinal));
                var joinData = joinNode?.Data.JoinData ?? new ParallelJoinDataPayload();
                string policy = (joinData.Policy ?? "all").Trim().ToLowerInvariant();

                if (policy == "n_of_m")
                {
                    int n = joinData.RequiredCount ?? joinData.QuorumCount ?? joinState.ExpectedCount;
                    if (nonFaultedCount < n)
                    {
                        CancelSiblingTokensAndTasks(instance, activeToken.SplitNodeId, activeToken.Id, node.Id);
                        instance.RecordFailure(node.Id, node.Data.NodeType, errorCode,
                            $"Cannot satisfy n_of_m ({n}) after branch failure: {errorMessage}", stackTrace, instance.ContextVariablesJson);
                    }
                }
                return;
            }
        }

        // "fail_instance" or "cancel_siblings" (or no remaining branches under continue_without)
        if (!string.IsNullOrWhiteSpace(activeToken.SplitNodeId))
        {
            CancelSiblingTokensAndTasks(instance, activeToken.SplitNodeId, activeToken.Id, node.Id);
        }

        instance.RecordFailure(node.Id, node.Data.NodeType, errorCode, errorMessage, stackTrace, instance.ContextVariablesJson);
    }

    private static List<object?> ResolveCollectionItems(string? expression, string? variablesJson)
    {
        var items = new List<object?>();
        if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(variablesJson))
        {
            return items;
        }

        string path = expression.Trim();
        var match = Regex.Match(path, @"^\{\{\s*(?:vars\.)?([a-zA-Z0-9_.-]+)\s*\}\}$");
        if (match.Success)
        {
            path = match.Groups[1].Value;
        }
        else if (path.StartsWith("$.", StringComparison.Ordinal))
        {
            path = path[2..];
        }
        else if (path.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
        {
            path = path[5..];
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (TryResolveProperty(doc.RootElement, path, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(ConvertJsonElement(item));
                }
            }
        }
        catch
        {
            // Malformed JSON or unresolvable expression -> empty collection
        }

        return items;
    }

    private static (bool Success, string? MergedJson, List<string> MergedKeys, string? ErrorMessage) MergeBranchVariables(
        string? baseVariablesJson,
        List<WorkflowToken> completedTokens,
        ParallelJoinDataPayload joinData)
    {
        var baseDict = ParseVariablesDictionary(baseVariablesJson);
        var mergedKeys = new List<string>();

        // Order deterministically by BranchId, CreatedAt, Id
        var deterministicTokens = completedTokens
            .OrderBy(t => t.BranchId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        // Collect writes per variable key across branches (excluding internal foreach loop metadata)
        var writesByKey = new Dictionary<string, List<(WorkflowToken Token, object? Value)>>(StringComparer.Ordinal);
        foreach (var token in deterministicTokens)
        {
            var branchDict = ParseVariablesDictionary(token.BranchVariablesJson);
            foreach (var kvp in branchDict)
            {
                if (string.Equals(kvp.Key, "itemIndex", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!writesByKey.TryGetValue(kvp.Key, out var list))
                {
                    list = new List<(WorkflowToken, object?)>();
                    writesByKey[kvp.Key] = list;
                }

                list.Add((token, kvp.Value));
            }
        }

        if (writesByKey.Count == 0)
        {
            return (true, null, mergedKeys, null);
        }

        foreach (var (key, entries) in writesByKey.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            mergedKeys.Add(key);

            if (entries.Count == 1)
            {
                baseDict[key] = entries[0].Value;
                continue;
            }

            string strategy = ResolveEffectiveMergeStrategy(key, joinData);

            switch (strategy)
            {
                case "error_on_conflict":
                {
                    string firstSerialized = JsonSerializer.Serialize(entries[0].Value);
                    bool anyDiff = entries.Skip(1).Any(e => !string.Equals(JsonSerializer.Serialize(e.Value), firstSerialized, StringComparison.Ordinal));
                    if (anyDiff)
                    {
                        return (false, null, mergedKeys,
                            $"Conflict merging variable '{key}' across parallel branches under strategy 'error_on_conflict'.");
                    }
                    baseDict[key] = entries[0].Value;
                    break;
                }

                case "last_writer_wins":
                {
                    var lastEntry = entries
                        .OrderBy(e => e.Token.CompletedAt ?? e.Token.UpdatedAt)
                        .ThenBy(e => e.Token.BranchId ?? string.Empty, StringComparer.Ordinal)
                        .ThenBy(e => e.Token.Id, StringComparer.Ordinal)
                        .Last();
                    baseDict[key] = lastEntry.Value;
                    break;
                }

                case "append":
                {
                    var list = new List<object?>();
                    foreach (var entry in entries)
                    {
                        if (entry.Value is List<object?> nestedList)
                        {
                            list.AddRange(nestedList);
                        }
                        else
                        {
                            list.Add(entry.Value);
                        }
                    }
                    baseDict[key] = list;
                    break;
                }

                case "sum":
                {
                    decimal sum = 0m;
                    foreach (var entry in entries)
                    {
                        if (TryConvertToDecimal(entry.Value, out decimal d))
                        {
                            sum += d;
                        }
                    }
                    baseDict[key] = sum;
                    break;
                }

                case "first_non_null":
                {
                    object? chosen = null;
                    foreach (var entry in entries)
                    {
                        if (entry.Value != null &&
                            !(entry.Value is string s && string.IsNullOrWhiteSpace(s)))
                        {
                            chosen = entry.Value;
                            break;
                        }
                    }
                    baseDict[key] = chosen;
                    break;
                }

                default:
                    baseDict[key] = entries[^1].Value;
                    break;
            }
        }

        return (true, JsonSerializer.Serialize(baseDict), mergedKeys, null);
    }

    private static string ResolveEffectiveMergeStrategy(string key, ParallelJoinDataPayload joinData)
    {
        if (joinData.VariableMergeStrategies != null)
        {
            var match = joinData.VariableMergeStrategies
                .FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value.Trim().ToLowerInvariant();
            }
        }

        return string.IsNullOrWhiteSpace(joinData.MergeStrategy)
            ? "error_on_conflict"
            : joinData.MergeStrategy.Trim().ToLowerInvariant();
    }

    private static void ApplySetVariables(
        WorkflowInstance instance,
        Dictionary<string, JsonElement> setVariables,
        WorkflowToken? activeToken)
    {
        string? effectiveVarsJson = BuildEffectiveVariablesJson(instance.ContextVariablesJson, activeToken?.BranchVariablesJson);
        var targetDict = activeToken != null
            ? ParseVariablesDictionary(activeToken.BranchVariablesJson)
            : ParseVariablesDictionary(instance.ContextVariablesJson);

        foreach (var kvp in setVariables)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

            object? resolvedValue = kvp.Value.ValueKind == JsonValueKind.String
                ? InterpolateTemplateWithVars(kvp.Value.GetString(), instance, effectiveVarsJson)
                : ConvertJsonElement(kvp.Value);

            targetDict[kvp.Key.Trim()] = resolvedValue;
        }

        string serialized = JsonSerializer.Serialize(targetDict);
        if (activeToken != null)
        {
            activeToken.UpdateBranchVariables(serialized);
        }
        else
        {
            instance.UpdateVariables(serialized);
        }
    }

    private static void CaptureBranchVariableDiff(
        WorkflowInstance instance,
        WorkflowToken activeToken,
        string? originalInstanceVars,
        string? preExecutionEffectiveVars)
    {
        var preDict = ParseVariablesDictionary(preExecutionEffectiveVars);
        var postDict = ParseVariablesDictionary(instance.ContextVariablesJson);
        var branchDict = ParseVariablesDictionary(activeToken.BranchVariablesJson);

        bool changed = false;
        foreach (var kvp in postDict)
        {
            string newSerialized = JsonSerializer.Serialize(kvp.Value);
            bool existed = preDict.TryGetValue(kvp.Key, out var oldVal);
            if (!existed || !string.Equals(JsonSerializer.Serialize(oldVal), newSerialized, StringComparison.Ordinal))
            {
                branchDict[kvp.Key] = kvp.Value;
                changed = true;
            }
        }

        // Restore instance-level context so branch writes stay isolated until Join merge
        if (!string.Equals(instance.ContextVariablesJson, originalInstanceVars, StringComparison.Ordinal))
        {
            instance.UpdateVariables(originalInstanceVars ?? "{}");
        }

        if (changed)
        {
            activeToken.UpdateBranchVariables(JsonSerializer.Serialize(branchDict));
        }
    }

    private static string? BuildEffectiveVariablesJson(string? instanceVariablesJson, string? branchVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(branchVariablesJson))
        {
            return instanceVariablesJson;
        }

        var merged = ParseVariablesDictionary(instanceVariablesJson);
        var branch = ParseVariablesDictionary(branchVariablesJson);
        foreach (var kvp in branch)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return JsonSerializer.Serialize(merged);
    }

    private static Dictionary<string, object?> ParseVariablesDictionary(string? json)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return dict;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }

        return dict;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : (element.TryGetDecimal(out var d) ? d : element.GetDouble()),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.Ordinal),
            _ => element.ToString()
        };
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case double db:
                result = Convert.ToDecimal(db);
                return true;
            case float f:
                result = Convert.ToDecimal(f);
                return true;
            case string s when decimal.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0m;
                return false;
        }
    }

    public static string InterpolateTemplateWithVars(string? template, WorkflowInstance instance, string? variablesJson)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template ?? string.Empty;
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
                doc = null;
            }
        }

        using (doc)
        {
            return Regex.Replace(template, @"\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}", match =>
            {
                string key = match.Groups[1].Value;
                if (key.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
                {
                    key = key[5..];
                }

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

        return instance.Tasks.Any(t => t.IsActive && t.NodeId == instance.CurrentNodeId);
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
