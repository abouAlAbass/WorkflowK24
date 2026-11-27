using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services.Executors;

public enum NodeExecutionOutcome
{
    Completed,
    Waiting,
    Faulted
}

public record NodeExecutionContext(
    WorkflowInstance Instance,
    ReactFlowGraph Graph,
    ReactFlowNode Node,
    bool IsSubWorkflow
);

public record NodeExecutionResult(
    NodeExecutionOutcome Outcome,
    string? ExplicitNextNodeId = null,
    string? SelectedOutcome = null,
    string? ErrorCode = null,
    string? ErrorMessage = null
)
{
    public static NodeExecutionResult Completed(string? explicitNextNodeId = null, string? selectedOutcome = null)
        => new(NodeExecutionOutcome.Completed, explicitNextNodeId, selectedOutcome);

    public static NodeExecutionResult Waiting()
        => new(NodeExecutionOutcome.Waiting);

    public static NodeExecutionResult Faulted(string errorCode, string errorMessage)
        => new(NodeExecutionOutcome.Faulted, ErrorCode: errorCode, ErrorMessage: errorMessage);
}

/// <summary>
/// Pluggable node executor contract for extending WorkflowExecutionEngine with custom node types.
/// </summary>
public interface INodeExecutor
{
    bool CanExecute(string nodeType);
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default);
}

/// <summary>
/// Registry resolving pluggable INodeExecutor implementations by node type.
/// </summary>
public interface INodeExecutorRegistry
{
    INodeExecutor? Resolve(string nodeType);
}

public class NodeExecutorRegistry : INodeExecutorRegistry
{
    private readonly IReadOnlyList<INodeExecutor> _executors;

    public NodeExecutorRegistry(IEnumerable<INodeExecutor> executors)
    {
        _executors = executors?.ToList() ?? new List<INodeExecutor>();
    }

    public INodeExecutor? Resolve(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return null;
        }

        return _executors.FirstOrDefault(e => e.CanExecute(nodeType));
    }
}
