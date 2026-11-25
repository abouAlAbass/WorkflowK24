using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Exceptions;

/// <summary>
/// Thrown when an illegal state transition is attempted on a workflow instance.
/// Mapped to HTTP 409 Conflict with code INVALID_TRANSITION.
/// </summary>
public class InvalidWorkflowTransitionException : Exception
{
    public const string ErrorCode = "INVALID_TRANSITION";

    public InvalidWorkflowTransitionException(string message) : base(message)
    {
    }

    public InvalidWorkflowTransitionException(WorkflowStatus currentStatus, WorkflowTrigger trigger)
        : base($"Cannot execute trigger '{trigger.ToString().ToLowerInvariant()}' on workflow instance in state '{currentStatus}'.")
    {
    }
}
