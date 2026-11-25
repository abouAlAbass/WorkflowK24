using System.Text.Json;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services;

/// <summary>
/// Creates and executes a workflow instance up to its first wait state WITHOUT committing,
/// so callers can compose it inside a larger unit of work (e.g. an atomic form submission).
/// </summary>
public interface IWorkflowInstanceStarter
{
    /// <exception cref="InvalidOperationException">No active published version exists for the workflow key.</exception>
    Task<WorkflowInstance> StartAsync(
        string workflowKey,
        string initiatorId,
        IReadOnlyDictionary<string, object>? triggerVariables,
        CancellationToken ct = default);
}

public class WorkflowInstanceStarter : IWorkflowInstanceStarter
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowExecutionEngine _executionEngine;

    public WorkflowInstanceStarter(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowExecutionEngine executionEngine)
    {
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
    }

    public async Task<WorkflowInstance> StartAsync(
        string workflowKey,
        string initiatorId,
        IReadOnlyDictionary<string, object>? triggerVariables,
        CancellationToken ct = default)
    {
        var activeVersion = await _definitionRepository.GetActiveVersionByKeyAsync(workflowKey, ct);
        if (activeVersion == null)
        {
            throw new InvalidOperationException($"No active workflow version found for key '{workflowKey}'.");
        }

        string instanceId = $"wf-inst-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        string? variablesJson = triggerVariables != null
            ? JsonSerializer.Serialize(triggerVariables.ToDictionary(k => k.Key, v => v.Value))
            : null;

        var instance = new WorkflowInstance(instanceId, activeVersion.VersionId, workflowKey, initiatorId, variablesJson);
        _instanceRepository.Add(instance);

        var graph = JsonSerializer.Deserialize<ReactFlowGraph>(activeVersion.GraphJson);
        if (graph != null)
        {
            var sensitiveVars = graph.Nodes
                .Where(n => n.Data.TriggerData?.Variables != null)
                .SelectMany(n => n.Data.TriggerData!.Variables!)
                .Where(v => v.IsSensitive && !string.IsNullOrWhiteSpace(v.Name))
                .Select(v => v.Name)
                .ToList();

            if (sensitiveVars.Count > 0)
            {
                instance.SetSensitiveVariableNames(sensitiveVars);
            }

            var triggerNode = graph.Nodes.FirstOrDefault(n => n.Data.NodeType.Equals("trigger", StringComparison.OrdinalIgnoreCase));
            if (triggerNode != null)
            {
                await _executionEngine.ExecuteNodeLogicAsync(instance, graph, triggerNode.Id, ct);
            }
        }

        return instance;
    }
}
