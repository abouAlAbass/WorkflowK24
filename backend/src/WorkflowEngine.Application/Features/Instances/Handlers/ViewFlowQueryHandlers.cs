using System.Text.Json;
using WorkflowEngine.Application.Common.Security;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Instances.Queries;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Services.Executors;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Instances.Handlers;

/// <summary>
/// Wolverine CQRS read handlers for ViewFlow projection and cursor-paginated InstanceEvents.
/// </summary>
public class ViewFlowQueryHandlers
{
    private const int DefaultEventsLimit = 50;
    private const int MaxEventsLimit = 200;

    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly INodeExecutionRepository _nodeExecutionRepository;
    private readonly IInstanceEventRepository _eventRepository;
    private readonly ISmartObjectRepository? _smartObjectRepository;

    public ViewFlowQueryHandlers(
        IWorkflowInstanceRepository instanceRepository,
        INodeExecutionRepository nodeExecutionRepository,
        IInstanceEventRepository eventRepository,
        ISmartObjectRepository? smartObjectRepository = null)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _nodeExecutionRepository = nodeExecutionRepository ?? throw new ArgumentNullException(nameof(nodeExecutionRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _smartObjectRepository = smartObjectRepository;
    }

    public async Task<ViewFlowDto?> Handle(GetInstanceViewFlowQuery query, CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdWithDetailsAsync(query.InstanceId, ct);
        if (instance == null)
        {
            return null;
        }

        var nodeExecutions = await _nodeExecutionRepository.GetByInstanceIdAsync(instance.InstanceId, ct);
        var events = await _eventRepository.GetByInstanceIdAsync(instance.InstanceId, ct);

        string definitionJson = instance.WorkflowVersion?.GraphJson ?? "{}";
        var explicitSensitiveNames = await CollectSensitiveVariableNamesAsync(instance, definitionJson, ct);
        bool canViewSensitive = SensitiveDataMasker.CanViewSensitive(query.Roles, query.Permissions);

        var instanceInfo = new ViewFlowInstanceDto(
            Id: instance.InstanceId,
            Status: instance.Status.ToString(),
            SuspendedFromStatus: instance.SuspendedFromStatus?.ToString(),
            StartedAt: instance.StartedAt,
            EndedAt: instance.CompletedAt,
            Version: instance.Version,
            DefinitionId: instance.WorkflowVersion?.DefinitionId ?? 0,
            DefinitionVersion: instance.WorkflowVersion?.VersionNumber ?? 0
        );

        var nodeDtos = nodeExecutions.Select(n => new ViewFlowNodeExecutionDto(
            NodeId: n.NodeId,
            Type: n.NodeType,
            Status: n.Status,
            Attempts: n.Attempts,
            StartedAt: n.StartedAt,
            EndedAt: n.EndedAt,
            Error: n.ErrorMessage
        )).ToList();

        var activeNodes = ComputeActiveNodes(instance, nodeExecutions);

        var tokenDtos = instance.Tokens
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.BranchId ?? string.Empty, StringComparer.Ordinal)
            .Select(t => new ViewFlowTokenDto(
                Id: t.Id,
                ParentTokenId: t.ParentTokenId,
                NodeId: t.NodeId,
                Status: t.Status.ToString(),
                BranchId: t.BranchId,
                SplitNodeId: t.SplitNodeId,
                Version: t.Version))
            .ToList();

        var joinStateDtos = instance.JoinStates
            .OrderBy(j => j.CreatedAt)
            .Select(j => new ViewFlowJoinStateDto(
                Id: j.Id,
                SplitNodeId: j.SplitNodeId,
                JoinNodeId: j.JoinNodeId,
                ExpectedCount: j.ExpectedCount,
                ArrivedCount: j.ArrivedCount,
                JoinFired: j.JoinFired,
                Version: j.Version))
            .ToList();

        var activeTokens = tokenDtos.Count > 0
            ? instance.Tokens
                .Where(t => t.IsOpen)
                .OrderBy(t => t.BranchId ?? string.Empty, StringComparer.Ordinal)
                .Select(t => string.IsNullOrWhiteSpace(t.BranchId) ? t.NodeId : $"{t.BranchId}:{t.NodeId}")
                .ToList()
            : activeNodes;

        var timelineDtos = events
            .OrderBy(e => e.Seq)
            .TakeLast(DefaultEventsLimit)
            .Select(e => MapEventDto(e, explicitSensitiveNames, canViewSensitive))
            .ToList();

        var variables = SensitiveDataMasker.MaskVariables(
            instance.ContextVariablesJson,
            explicitSensitiveNames,
            canViewSensitive);

        return new ViewFlowDto(
            Instance: instanceInfo,
            DefinitionJson: definitionJson,
            Nodes: nodeDtos,
            ActiveNodes: activeNodes,
            ActiveTokens: activeTokens,
            Timeline: timelineDtos,
            Variables: variables,
            Tokens: tokenDtos,
            JoinStates: joinStateDtos
        );
    }

    public async Task<InstanceEventsPageDto?> Handle(GetInstanceEventsQuery query, CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdAsync(query.InstanceId, ct);
        if (instance == null)
        {
            return null;
        }

        int afterSeq = Math.Max(0, query.AfterSeq);
        int boundedLimit = Math.Clamp(query.Limit <= 0 ? DefaultEventsLimit : query.Limit, 1, MaxEventsLimit);

        // Fetch one extra item to determine whether more pages exist
        var fetched = await _eventRepository.GetByInstanceIdCursorAsync(instance.InstanceId, afterSeq, boundedLimit + 1, ct);
        bool hasMore = fetched.Count > boundedLimit;
        var pageItems = hasMore ? fetched.Take(boundedLimit).ToList() : fetched.ToList();

        string definitionJson = instance.WorkflowVersion?.GraphJson ?? "{}";
        var explicitSensitiveNames = await CollectSensitiveVariableNamesAsync(instance, definitionJson, ct);
        bool canViewSensitive = SensitiveDataMasker.CanViewSensitive(query.Roles, query.Permissions);

        var eventDtos = pageItems
            .Select(e => MapEventDto(e, explicitSensitiveNames, canViewSensitive))
            .ToList();

        int? nextSeq = eventDtos.Count > 0 ? eventDtos[^1].Seq : null;

        return new InstanceEventsPageDto(
            InstanceId: instance.InstanceId,
            AfterSeq: afterSeq,
            Limit: boundedLimit,
            NextSeq: nextSeq,
            HasMore: hasMore,
            Events: eventDtos
        );
    }

    private static InstanceEventDto MapEventDto(
        InstanceEvent e,
        ISet<string> explicitSensitiveNames,
        bool canViewSensitive)
    {
        return new InstanceEventDto(
            Id: e.Id,
            InstanceId: e.InstanceId,
            Seq: e.Seq,
            Type: e.Type,
            NodeId: e.NodeId,
            ActorId: e.ActorId,
            ActorType: e.ActorType,
            PayloadJson: SensitiveDataMasker.MaskPayloadJson(e.PayloadJson, explicitSensitiveNames, canViewSensitive),
            CreatedAt: e.CreatedAt
        );
    }

    private static List<string> ComputeActiveNodes(WorkflowInstance instance, IReadOnlyList<NodeExecution> nodeExecutions)
    {
        if (instance.Status is WorkflowStatus.Completed or WorkflowStatus.Cancelled)
        {
            return new List<string>();
        }

        var active = new List<string>();
        foreach (var n in nodeExecutions)
        {
            if ((n.Status is "Running" or "Waiting") && !active.Contains(n.NodeId, StringComparer.OrdinalIgnoreCase))
            {
                active.Add(n.NodeId);
            }
        }

        if (!string.IsNullOrWhiteSpace(instance.CurrentNodeId) &&
            !active.Contains(instance.CurrentNodeId, StringComparer.OrdinalIgnoreCase))
        {
            active.Add(instance.CurrentNodeId);
        }

        return active;
    }

    private async Task<HashSet<string>> CollectSensitiveVariableNamesAsync(
        WorkflowInstance instance,
        string? definitionJson,
        CancellationToken ct)
    {
        var names = new HashSet<string>(instance.GetSensitiveVariableNames(), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(definitionJson))
        {
            try
            {
                var graph = JsonSerializer.Deserialize<ReactFlowGraph>(definitionJson);
                if (graph?.Nodes != null)
                {
                    foreach (var node in graph.Nodes)
                    {
                        var vars = node.Data.TriggerData?.Variables;
                        if (vars != null)
                        {
                            foreach (var v in vars)
                            {
                                if (v.IsSensitive && !string.IsNullOrWhiteSpace(v.Name))
                                {
                                    names.Add(v.Name.Trim());
                                }
                            }
                        }

                        var soData = node.Data.SmartObjectData;
                        if (_smartObjectRepository != null &&
                            soData != null &&
                            SmartObjectNodeExecutor.TryParseSmartObjectRef(soData.SmartObjectRef, out var soKey, out var soVer))
                        {
                            var so = await _smartObjectRepository.GetByNameAndVersionAsync(soKey, soVer, ct);
                            if (so != null)
                            {
                                var maskedFields = SmartObjectNodeExecutor.DeserializeFields(so.FieldsJson)
                                    .Where(f => f.Masked && !string.IsNullOrWhiteSpace(f.Name))
                                    .Select(f => f.Name.Trim())
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                                names.UnionWith(maskedFields);

                                if (soData.OutputMapping != null && maskedFields.Count > 0)
                                {
                                    foreach (var kv in soData.OutputMapping)
                                    {
                                        string targetVar = kv.Key?.Trim() ?? string.Empty;
                                        string sourceExpr = kv.Value?.Trim() ?? string.Empty;
                                        string normalizedSource = sourceExpr.StartsWith("$.", StringComparison.Ordinal)
                                            ? sourceExpr[2..]
                                            : sourceExpr;
                                        if (!string.IsNullOrWhiteSpace(targetVar) &&
                                            (maskedFields.Contains(sourceExpr) || maskedFields.Contains(normalizedSource)))
                                        {
                                            names.Add(targetVar);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed graph JSON
            }
        }

        return names;
    }
}
