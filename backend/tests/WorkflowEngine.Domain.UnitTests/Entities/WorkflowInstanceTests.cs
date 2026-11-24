using FluentAssertions;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.UnitTests.Entities;

public class WorkflowInstanceTests
{
    [Fact]
    public void Constructor_InitializesRunningInstanceWithAuditLog()
    {
        // Act
        var instance = new WorkflowInstance(
            "inst-001",
            1,
            "onboarding",
            "initiator@test.com",
            "{\"amount\": 100}");

        // Assert
        instance.InstanceId.Should().Be("inst-001");
        instance.VersionId.Should().Be(1);
        instance.WorkflowKey.Should().Be("onboarding");
        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.AuditLogs.Should().HaveCount(1);
        instance.AuditLogs.First().EventType.Should().Be(AuditEventType.NodeEnter);
    }

    [Fact]
    public void TransitionToNode_WhenEndNode_CompletesInstance()
    {
        // Arrange
        var instance = new WorkflowInstance("inst-002", 1, "test", "user", null);

        // Act
        instance.TransitionToNode("node-end", "end");

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.CompletedAt.Should().NotBeNull();
        instance.CurrentNodeId.Should().Be("node-end");
    }

    [Fact]
    public void CreateTask_AddsPendingTaskAndLogsAudit()
    {
        // Arrange
        var instance = new WorkflowInstance("inst-003", 1, "test", "user", null);

        // Act
        var task = instance.CreateTask("task-1", "node-appr", "Manager Approval", AssigneeType.User, "mgr@test.com", 3);

        // Assert
        instance.Tasks.Should().ContainSingle();
        task.TaskId.Should().Be("task-1");
        task.Status.Should().Be(TaskStatus.Pending);
        task.AssigneeId.Should().Be("mgr@test.com");
    }

    [Fact]
    public void RecordFailure_MarksStatusFailedAndAddsError()
    {
        // Arrange
        var instance = new WorkflowInstance("inst-004", 1, "test", "user", null);

        // Act
        instance.RecordFailure("node-api", "service", "ERR_500", "Service unavailable", "stack", null);

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.Errors.Should().ContainSingle();
        instance.Errors.First().ErrorCode.Should().Be("ERR_500");
    }
}
