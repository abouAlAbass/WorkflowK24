using FluentAssertions;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.UnitTests.Entities;

public class WorkflowInstanceStateMachineTests
{
    [Theory]
    [InlineData(WorkflowStatus.Created, WorkflowTrigger.Start, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Running, WorkflowTrigger.Wait, WorkflowStatus.Waiting)]
    [InlineData(WorkflowStatus.Waiting, WorkflowTrigger.Continue, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Running, WorkflowTrigger.Suspend, WorkflowStatus.Suspended)]
    [InlineData(WorkflowStatus.Waiting, WorkflowTrigger.Suspend, WorkflowStatus.Suspended)]
    [InlineData(WorkflowStatus.Running, WorkflowTrigger.Fault, WorkflowStatus.Faulted)]
    [InlineData(WorkflowStatus.Faulted, WorkflowTrigger.Retry, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Running, WorkflowTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Waiting, WorkflowTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Suspended, WorkflowTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Faulted, WorkflowTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Running, WorkflowTrigger.Complete, WorkflowStatus.Completed)]
    public void CanTransition_And_GetNextStatus_AllowedTransitions(
        WorkflowStatus current,
        WorkflowTrigger trigger,
        WorkflowStatus expectedNext)
    {
        WorkflowInstanceStateMachine.CanTransition(current, trigger).Should().BeTrue();
        WorkflowInstanceStateMachine.GetNextStatus(current, trigger).Should().Be(expectedNext);
    }

    [Theory]
    [InlineData(WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Waiting)]
    public void Resume_RestoresSuspendedFromStatus(WorkflowStatus suspendedFrom)
    {
        WorkflowInstanceStateMachine.CanTransition(WorkflowStatus.Suspended, WorkflowTrigger.Resume, suspendedFrom).Should().BeTrue();
        WorkflowInstanceStateMachine.GetNextStatus(WorkflowStatus.Suspended, WorkflowTrigger.Resume, suspendedFrom).Should().Be(suspendedFrom);
    }

    [Theory]
    [InlineData(WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Cancelled)]
    public void TerminalStates_RefuseAnyOutgoingTransitionWithInvalidTransition(WorkflowStatus terminal)
    {
        var allTriggers = Enum.GetValues<WorkflowTrigger>();

        foreach (var trigger in allTriggers)
        {
            WorkflowInstanceStateMachine.CanTransition(terminal, trigger).Should().BeFalse();

            Action act = () => WorkflowInstanceStateMachine.ValidateTransition(terminal, trigger);
            act.Should().Throw<InvalidWorkflowTransitionException>()
                .Where(ex => ex.Message.Contains(trigger.ToString().ToLowerInvariant()));
        }
    }

    [Fact]
    public void DisallowedTransitions_ThrowInvalidWorkflowTransitionException()
    {
        // Created cannot Suspend, Cancel, Complete, Fault, Retry
        Action act1 = () => WorkflowInstanceStateMachine.ValidateTransition(WorkflowStatus.Created, WorkflowTrigger.Suspend);
        act1.Should().Throw<InvalidWorkflowTransitionException>();

        // Faulted cannot Complete, Suspend, Wait
        Action act2 = () => WorkflowInstanceStateMachine.ValidateTransition(WorkflowStatus.Faulted, WorkflowTrigger.Complete);
        act2.Should().Throw<InvalidWorkflowTransitionException>();

        // Suspended cannot Complete, Fault, Retry
        Action act3 = () => WorkflowInstanceStateMachine.ValidateTransition(WorkflowStatus.Suspended, WorkflowTrigger.Complete);
        act3.Should().Throw<InvalidWorkflowTransitionException>();

        // Running cannot Retry or Resume
        Action act4 = () => WorkflowInstanceStateMachine.ValidateTransition(WorkflowStatus.Running, WorkflowTrigger.Retry);
        act4.Should().Throw<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public void WorkflowInstance_SuspendAndResume_UpdatesStatusAndVersion()
    {
        var instance = new WorkflowInstance("inst-test-1", 1, "test-wf", "initiator@corp.internal", null);
        instance.Status.Should().Be(WorkflowStatus.Running);
        int initialVersion = instance.Version;

        instance.Suspend("Maintenance period");
        instance.Status.Should().Be(WorkflowStatus.Suspended);
        instance.SuspendedFromStatus.Should().Be(WorkflowStatus.Running);
        instance.SuspendedAt.Should().NotBeNull();
        instance.Version.Should().Be(initialVersion + 1);

        instance.Resume("Maintenance completed");
        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.SuspendedFromStatus.Should().BeNull();
        instance.SuspendedAt.Should().BeNull();
        instance.Version.Should().Be(initialVersion + 2);
    }

    [Fact]
    public void WorkflowInstance_Cancel_CancelsOpenPendingTasks()
    {
        var instance = new WorkflowInstance("inst-test-2", 1, "test-wf", "initiator@corp.internal", null);
        var task1 = instance.CreateTask("task-1", "node-1", "Review A", AssigneeType.User, "user1@corp.internal", 1);
        var task2 = instance.CreateTask("task-2", "node-2", "Review B", AssigneeType.User, "user2@corp.internal", 1);

        task1.Status.Should().Be(TaskStatus.Pending);
        task2.Status.Should().Be(TaskStatus.Pending);

        instance.Cancel("Cancelled due to business priority change");

        instance.Status.Should().Be(WorkflowStatus.Cancelled);
        instance.CompletedAt.Should().NotBeNull();
        task1.Status.Should().Be(TaskStatus.Cancelled);
        task2.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public void WorkflowInstance_Retry_WhenFaulted_TransitionsToRunning()
    {
        var instance = new WorkflowInstance("inst-test-3", 1, "test-wf", "initiator@corp.internal", null);
        instance.RecordFailure("node-err", "service", "ERR_500", "Remote failure", null, null);
        instance.Status.Should().Be(WorkflowStatus.Faulted);

        instance.Retry("Retry requested after bugfix", fromNodeId: "node-err");
        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.CurrentNodeId.Should().Be("node-err");
    }

    [Fact]
    public void WorkflowInstance_Retry_WhenNotFaulted_ThrowsInvalidWorkflowTransition()
    {
        var instance = new WorkflowInstance("inst-test-4", 1, "test-wf", "initiator@corp.internal", null);
        instance.Status.Should().Be(WorkflowStatus.Running);

        Action act = () => instance.Retry("premature retry");
        act.Should().Throw<InvalidWorkflowTransitionException>();
    }
}
