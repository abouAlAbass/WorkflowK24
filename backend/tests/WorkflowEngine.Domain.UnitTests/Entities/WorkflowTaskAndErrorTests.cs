using FluentAssertions;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.UnitTests.Entities;

public class WorkflowTaskTests
{
    [Fact]
    public void Complete_WithApproval_SetsStatusApproved()
    {
        // Arrange
        var task = new WorkflowTask(
            "task-01", "inst-01", "node-1", "Review Request", AssigneeType.User, "user@test.com", null);

        // Act
        task.Complete("Approve", "user@test.com", "Looks good");

        // Assert
        task.Status.Should().Be(TaskStatus.Approved);
        task.SelectedOutcome.Should().Be("Approve");
        task.CompletedBy.Should().Be("user@test.com");
        task.Comments.Should().Be("Looks good");
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithReject_SetsStatusRejected()
    {
        // Arrange
        var task = new WorkflowTask(
            "task-02", "inst-01", "node-1", "Review Request", AssigneeType.User, "user@test.com", null);

        // Act
        task.Complete("Reject", "user@test.com", "Budget exceeded");

        // Assert
        task.Status.Should().Be(TaskStatus.Rejected);
        task.SelectedOutcome.Should().Be("Reject");
    }

    [Fact]
    public void Delegate_UpdatesAssigneeAndSetsStatusDelegated()
    {
        // Arrange
        var task = new WorkflowTask(
            "task-03", "inst-01", "node-1", "Review Request", AssigneeType.User, "user@test.com", null);

        // Act
        task.Delegate("peer@test.com", AssigneeType.User, "user@test.com", "Delegated to peer");

        // Assert
        task.Status.Should().Be(TaskStatus.Delegated);
        task.AssigneeId.Should().Be("peer@test.com");
    }
}

public class WorkflowErrorTests
{
    [Fact]
    public void Resolve_MarksIsResolvedTrueAndSetsResolvedAt()
    {
        // Arrange
        var error = new WorkflowError(
            "inst-01", "node-1", "sendEmail", "SMTP_TIMEOUT", "Connection timed out", null, null);

        // Act
        error.Resolve();

        // Assert
        error.IsResolved.Should().BeTrue();
        error.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void IncrementRetry_IncreasesCounter()
    {
        // Arrange
        var error = new WorkflowError(
            "inst-01", "node-1", "sendEmail", "SMTP_TIMEOUT", "Connection timed out", null, null);

        // Act
        error.IncrementRetry();

        // Assert
        error.RetryCount.Should().Be(1);
    }
}
