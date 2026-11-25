using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Centralized explicit state machine for workflow instance transitions.
/// </summary>
public static class WorkflowInstanceStateMachine
{
    private static readonly HashSet<WorkflowStatus> TerminalStates = new()
    {
        WorkflowStatus.Cancelled,
        WorkflowStatus.Completed
    };

    public static bool CanTransition(
        WorkflowStatus currentStatus,
        WorkflowTrigger trigger,
        WorkflowStatus? suspendedFromStatus = null)
    {
        if (TerminalStates.Contains(currentStatus))
        {
            return false;
        }

        return (currentStatus, trigger) switch
        {
            (WorkflowStatus.Created, WorkflowTrigger.Start) => true,
            (WorkflowStatus.Running, WorkflowTrigger.Wait) => true,
            (WorkflowStatus.Waiting, WorkflowTrigger.Continue) => true,
            (WorkflowStatus.Running, WorkflowTrigger.Suspend) => true,
            (WorkflowStatus.Waiting, WorkflowTrigger.Suspend) => true,
            (WorkflowStatus.Suspended, WorkflowTrigger.Resume) =>
                suspendedFromStatus == null ||
                suspendedFromStatus == WorkflowStatus.Running ||
                suspendedFromStatus == WorkflowStatus.Waiting,
            (WorkflowStatus.Running, WorkflowTrigger.Fault) => true,
            (WorkflowStatus.Faulted, WorkflowTrigger.Retry) => true,
            (WorkflowStatus.Running, WorkflowTrigger.Cancel) => true,
            (WorkflowStatus.Waiting, WorkflowTrigger.Cancel) => true,
            (WorkflowStatus.Suspended, WorkflowTrigger.Cancel) => true,
            (WorkflowStatus.Faulted, WorkflowTrigger.Cancel) => true,
            (WorkflowStatus.Running, WorkflowTrigger.Complete) => true,
            _ => false
        };
    }

    public static WorkflowStatus GetNextStatus(
        WorkflowStatus currentStatus,
        WorkflowTrigger trigger,
        WorkflowStatus? suspendedFromStatus = null)
    {
        if (!CanTransition(currentStatus, trigger, suspendedFromStatus))
        {
            throw new InvalidWorkflowTransitionException(currentStatus, trigger);
        }

        return (currentStatus, trigger) switch
        {
            (WorkflowStatus.Created, WorkflowTrigger.Start) => WorkflowStatus.Running,
            (WorkflowStatus.Running, WorkflowTrigger.Wait) => WorkflowStatus.Waiting,
            (WorkflowStatus.Waiting, WorkflowTrigger.Continue) => WorkflowStatus.Running,
            (WorkflowStatus.Running, WorkflowTrigger.Suspend) => WorkflowStatus.Suspended,
            (WorkflowStatus.Waiting, WorkflowTrigger.Suspend) => WorkflowStatus.Suspended,
            (WorkflowStatus.Suspended, WorkflowTrigger.Resume) => suspendedFromStatus ?? WorkflowStatus.Running,
            (WorkflowStatus.Running, WorkflowTrigger.Fault) => WorkflowStatus.Faulted,
            (WorkflowStatus.Faulted, WorkflowTrigger.Retry) => WorkflowStatus.Running,
            (WorkflowStatus.Running, WorkflowTrigger.Cancel) => WorkflowStatus.Cancelled,
            (WorkflowStatus.Waiting, WorkflowTrigger.Cancel) => WorkflowStatus.Cancelled,
            (WorkflowStatus.Suspended, WorkflowTrigger.Cancel) => WorkflowStatus.Cancelled,
            (WorkflowStatus.Faulted, WorkflowTrigger.Cancel) => WorkflowStatus.Cancelled,
            (WorkflowStatus.Running, WorkflowTrigger.Complete) => WorkflowStatus.Completed,
            _ => throw new InvalidWorkflowTransitionException(currentStatus, trigger)
        };
    }

    public static void ValidateTransition(
        WorkflowStatus currentStatus,
        WorkflowTrigger trigger,
        WorkflowStatus? suspendedFromStatus = null)
    {
        if (!CanTransition(currentStatus, trigger, suspendedFromStatus))
        {
            throw new InvalidWorkflowTransitionException(currentStatus, trigger);
        }
    }
}
