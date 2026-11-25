using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services;

/// <summary>
/// Port for evaluating graph transitions and executing node logic.
/// </summary>
public interface IWorkflowExecutionEngine
{
    string? EvaluateNextNode(ReactFlowGraph graph, string currentNodeId, string? selectedOutcome = null, string? variablesJson = null);
    Task ExecuteNodeLogicAsync(WorkflowInstance instance, ReactFlowGraph graph, string nodeId, CancellationToken ct = default);
    Task<string?> ResumeAfterTaskAsync(WorkflowInstance instance, ReactFlowGraph rootGraph, string completedNodeId, string selectedOutcome, CancellationToken ct = default);
}
